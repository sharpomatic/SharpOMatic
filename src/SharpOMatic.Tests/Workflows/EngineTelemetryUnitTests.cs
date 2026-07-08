using System.Diagnostics;

namespace SharpOMatic.Tests.Workflows;

public sealed class EngineTelemetryUnitTests
{
    [Fact]
    public async Task Run_emits_invoke_agent_and_node_activities()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var workflow = new WorkflowBuilder().AddStart().AddEnd().Connect("start", "end").Build();
        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        // The run activity completes after the awaited run result is returned, so wait for it.
        var runActivity = await WaitForActivity(stopped, activity => activity.OperationName.StartsWith("invoke_agent") && HasRunTag(activity, run.RunId));
        Assert.Equal(ActivityStatusCode.Ok, runActivity.Status);
        Assert.Equal("invoke_agent", runActivity.GetTagItem("gen_ai.operation.name")?.ToString());
        Assert.Equal(workflow.Name, runActivity.GetTagItem("gen_ai.agent.name")?.ToString());
        Assert.Equal(run.WorkflowId.ToString(), runActivity.GetTagItem("gen_ai.agent.id")?.ToString());
        Assert.Equal(run.WorkflowId.ToString(), runActivity.GetTagItem("workflow.id")?.ToString());
        Assert.Equal(workflow.Name, runActivity.GetTagItem("workflow.name")?.ToString());
        Assert.Equal(nameof(RunStatus.Success), runActivity.GetTagItem("sharpomatic.run.status")?.ToString());

        var nodeActivities = stopped.Where(activity => activity.OperationName.StartsWith("executor.process") && HasRunTag(activity, run.RunId)).ToList();
        Assert.Equal(2, nodeActivities.Count);
        Assert.All(nodeActivities, activity => Assert.Equal(runActivity.SpanId, activity.ParentSpanId));
        Assert.All(nodeActivities, activity => Assert.Equal(runActivity.TraceId, activity.TraceId));
        Assert.All(nodeActivities, activity => Assert.Equal(ActivityStatusCode.Ok, activity.Status));
    }

    [Fact]
    public async Task Failed_run_marks_activities_with_error_status()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("bad", "throw new System.InvalidOperationException(\"boom\");")
            .AddEnd()
            .Connect("start", "bad")
            .Connect("bad", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);

        var runActivity = await WaitForActivity(stopped, activity => activity.OperationName.StartsWith("invoke_agent") && HasRunTag(activity, run.RunId));
        Assert.Equal(ActivityStatusCode.Error, runActivity.Status);
        Assert.Equal(nameof(RunStatus.Failed), runActivity.GetTagItem("sharpomatic.run.status")?.ToString());

        var failedNode = stopped.Single(activity => activity.OperationName.StartsWith("executor.process") && HasRunTag(activity, run.RunId) && activity.Status == ActivityStatusCode.Error);
        Assert.Equal(runActivity.SpanId, failedNode.ParentSpanId);
        Assert.Equal(nameof(NodeStatus.Failed), failedNode.GetTagItem("sharpomatic.node.status")?.ToString());
    }

    [Fact]
    public async Task Switch_node_activity_records_selected_branch()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "false"), new WorkflowBuilder.SwitchChoice("second", "true"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("first", "Context.Set<string>(\"result\", \"first\");")
            .AddCode("second", "Context.Set<string>(\"result\", \"second\");")
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.first", "first")
            .Connect("switch.second", "second")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        var switchActivity = await WaitForActivity(stopped, activity => activity.OperationName == "executor.process switch" && HasRunTag(activity, run.RunId));
        Assert.Equal("second", switchActivity.GetTagItem("sharpomatic.switch.selected")?.ToString());
    }

    [Fact]
    public async Task FanOut_and_fanin_activities_record_branch_counts()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "Context.Set<int>(\"output\", 1);")
            .AddCode("second", "Context.Set<int>(\"output\", 2);")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        var fanOutActivity = await WaitForActivity(stopped, activity => activity.OperationName == "executor.process fanout" && HasRunTag(activity, run.RunId));
        Assert.Equal("2", fanOutActivity.GetTagItem("sharpomatic.fan_out.branch_count")?.ToString());

        var fanInActivities = stopped.Where(activity => activity.OperationName == "executor.process fanin" && HasRunTag(activity, run.RunId)).ToList();
        Assert.Equal(2, fanInActivities.Count);
        Assert.Single(fanInActivities, activity => activity.GetTagItem("sharpomatic.fan_in.completed")?.ToString() == bool.TrueString);
    }

    [Fact]
    public async Task Disabled_telemetry_emits_no_activities()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var workflow = new WorkflowBuilder().AddStart().AddEnd().Connect("start", "end").Build();

        using var provider = WorkflowRunner.BuildProvider(services => services.Configure<SharpOMaticTelemetryOptions>(options => options.Enabled = false));
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engine.StartWorkflowRunAndWait(workflow.Id, []);
            await Task.Delay(200);

            Assert.True(run.RunStatus == RunStatus.Success, run.Error);
            Assert.DoesNotContain(stopped, activity => HasRunTag(activity, run.RunId));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static ActivityListener CreateListener(ConcurrentBag<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SharpOMaticDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static bool HasRunTag(Activity activity, Guid runId)
    {
        return activity.GetTagItem("sharpomatic.run.id")?.ToString() == runId.ToString();
    }

    private static async Task<Activity> WaitForActivity(ConcurrentBag<Activity> activities, Func<Activity, bool> predicate)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var match = activities.FirstOrDefault(predicate);
            if (match is not null)
                return match;

            await Task.Delay(50);
        }

        throw new TimeoutException("Expected activity was not emitted.");
    }
}
