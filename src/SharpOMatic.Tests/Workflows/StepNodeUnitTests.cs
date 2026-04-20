namespace SharpOMatic.Tests.Workflows;

public sealed class StepNodeUnitTests
{
    [Fact]
    public async Task Step_start_emits_step_start_event_and_continues()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddStepStart(stepName: "Search")
            .AddCode("after", """Context.Set<string>("output.route", "continued");""")
            .AddEnd()
            .Connect("start", "Step Start")
            .Connect("Step Start", "after")
            .Connect("after", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("continued", output.Get<string>("output.route"));

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            var stepEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.StepStart, stepEvent.EventKind);
            Assert.Equal("Search", stepEvent.TextDelta);
            Assert.Null(stepEvent.MessageId);
            Assert.Null(stepEvent.ToolCallId);
            Assert.Null(stepEvent.ParentMessageId);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Step_end_emits_step_end_event_and_continues()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddStepEnd(stepName: "Search")
            .AddCode("after", """Context.Set<string>("output.route", "continued");""")
            .AddEnd()
            .Connect("start", "Step End")
            .Connect("Step End", "after")
            .Connect("after", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("continued", output.Get<string>("output.route"));

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            var stepEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.StepEnd, stepEvent.EventKind);
            Assert.Equal("Search", stepEvent.TextDelta);
            Assert.Null(stepEvent.MessageId);
            Assert.Null(stepEvent.ToolCallId);
            Assert.Null(stepEvent.ParentMessageId);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Step_start_rejects_blank_step_name()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddStepStart(stepName: "")
            .Connect("start", "Step Start")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Step name must be a non-empty string.", run.Error);
    }

    [Fact]
    public async Task Step_end_rejects_whitespace_step_name()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddStepEnd(stepName: "   ")
            .Connect("start", "Step End")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Step name must be a non-empty string.", run.Error);
    }
}
