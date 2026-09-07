using System.Diagnostics;

namespace SharpOMatic.Tests.Workflows;

public sealed class EngineTelemetryUnitTests
{
    [Fact]
    public async Task Run_emits_workflow_and_node_activities()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var workflow = new WorkflowBuilder().AddStart().AddEnd().Connect("start", "end").Build();
        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        // The run activity completes after the awaited run result is returned, so wait for it.
        var runActivity = await WaitForActivity(stopped, activity => activity.OperationName.StartsWith("workflow") && HasRunTag(activity, run.RunId));
        Assert.Equal(ActivityStatusCode.Ok, runActivity.Status);
        Assert.Equal($"workflow {workflow.Name}", runActivity.DisplayName);
        Assert.Equal(run.WorkflowId.ToString(), runActivity.GetTagItem("workflow.id")?.ToString());
        Assert.Equal(workflow.Name, runActivity.GetTagItem("workflow.name")?.ToString());
        Assert.Equal(nameof(RunStatus.Success), runActivity.GetTagItem("sharpomatic.run.status")?.ToString());

        // A run drives a statically authored graph, so it must not be classified as a GenAI agent
        // invocation. Only the nested model call activities carry gen_ai.operation.name.
        Assert.Null(runActivity.GetTagItem("gen_ai.operation.name"));
        Assert.Null(runActivity.GetTagItem("gen_ai.agent.id"));
        Assert.Null(runActivity.GetTagItem("gen_ai.agent.name"));

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

        var workflow = new WorkflowBuilder().AddStart().AddCode("bad", "throw new System.InvalidOperationException(\"boom\");").AddEnd().Connect("start", "bad").Connect("bad", "end").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);

        var runActivity = await WaitForActivity(stopped, activity => activity.OperationName.StartsWith("workflow") && HasRunTag(activity, run.RunId));
        Assert.Equal(ActivityStatusCode.Error, runActivity.Status);
        Assert.Equal(nameof(RunStatus.Failed), runActivity.GetTagItem("sharpomatic.run.status")?.ToString());
        Assert.Equal(typeof(SharpOMaticException).FullName, runActivity.GetTagItem("error.type")?.ToString());

        var failedNode = stopped.Single(activity => activity.OperationName.StartsWith("executor.process") && HasRunTag(activity, run.RunId) && activity.Status == ActivityStatusCode.Error);
        Assert.Equal(runActivity.SpanId, failedNode.ParentSpanId);
        Assert.Equal(nameof(NodeStatus.Failed), failedNode.GetTagItem("sharpomatic.node.status")?.ToString());
        Assert.Equal(typeof(SharpOMaticException).FullName, failedNode.GetTagItem("error.type")?.ToString());

        var exceptionEvent = Assert.Single(failedNode.Events, activityEvent => activityEvent.Name == "exception");
        var exceptionTags = exceptionEvent.Tags.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString());
        Assert.Equal(typeof(SharpOMaticException).FullName, exceptionTags["exception.type"]);
        Assert.Contains("boom", exceptionTags["exception.message"]);
        Assert.Contains("SharpOMaticException", exceptionTags["exception.stacktrace"]);
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

    [Fact]
    public async Task Model_call_emits_agent_activity_around_each_provider_turn()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var caller = new TelemetryTestModelCaller();
        var chatClient = new TwoTurnToolChatClient();
        var services = new ServiceCollection()
            .AddSingleton<IOptions<SharpOMaticTelemetryOptions>>(new OptionsWrapper<SharpOMaticTelemetryOptions>(new SharpOMaticTelemetryOptions()))
            .BuildServiceProvider();

        var agent = caller.InvokeApplyAgentTelemetry(new ChatClientAgent(caller.InvokeCreateFunctionInvokingChatClient(chatClient, services), name: "Ask model", services: services), services);

        await caller.InvokeCallConfiguredAgent(agent, [AIFunctionFactory.Create(() => "sunny", "get_weather")]);

        // The model, not the engine, decided to take a second turn after the tool result, so the agent
        // activity is what spans the whole call while each chat activity covers one provider round trip.
        // The agent middleware reuses the "chat" operation name, so gen_ai.operation.name is what
        // distinguishes the two activity kinds (it is also what a GenAI-aware backend keys off).
        var agentActivity = await WaitForActivity(stopped, activity => IsOperation(activity, "invoke_agent"));
        Assert.StartsWith("invoke_agent Ask model", agentActivity.DisplayName);
        Assert.Equal("Ask model", agentActivity.GetTagItem("gen_ai.agent.name")?.ToString());

        var chatActivities = stopped.Where(activity => IsOperation(activity, "chat")).ToList();
        Assert.Equal(2, chatActivities.Count);
        Assert.All(chatActivities, activity => Assert.Equal(agentActivity.SpanId, activity.ParentSpanId));
        Assert.All(chatActivities, activity => Assert.Equal(agentActivity.TraceId, activity.TraceId));

        var toolActivity = Assert.Single(stopped, activity => activity.OperationName.StartsWith("execute_tool"));
        Assert.Equal(agentActivity.TraceId, toolActivity.TraceId);
    }

    private sealed class TelemetryTestModelCaller : BaseModelCaller
    {
        public AIAgent InvokeApplyAgentTelemetry(AIAgent agent, IServiceProvider serviceProvider) => ApplyAgentTelemetry(agent, serviceProvider);

        public IChatClient InvokeCreateFunctionInvokingChatClient(IChatClient chatClient, IServiceProvider serviceProvider) => CreateFunctionInvokingChatClient(chatClient, serviceProvider);

        public Task<ModelCallResult> InvokeCallConfiguredAgent(AIAgent agent, IList<AITool> tools)
        {
            var node = new ModelCallNodeEntity
            {
                Id = Guid.NewGuid(),
                Version = 1,
                NodeType = NodeType.ModelCall,
                Title = "Ask model",
                Top = 0,
                Left = 0,
                Width = 80,
                Height = 80,
                Inputs = [],
                Outputs = [],
                ToolContextPaths = [],
                ModelId = Guid.NewGuid(),
                Instructions = "",
                Prompt = "",
                ChatInputPath = "",
                ChatOutputPath = "",
                TextOutputPath = "",
                ImageInputPath = "",
                ImageOutputPath = "",
                BatchOutput = true,
            };
            return CallConfiguredAgent(agent, [], new ChatOptions() { Tools = tools }, jsonOutput: false, node, new TelemetryNullProgressSink());
        }

        public override Task<ModelCallResult> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        ) => throw new NotSupportedException();
    }

    /// <summary>
    /// Requests a tool on the first turn and answers with text on the second, so a single model call
    /// produces two provider round trips the way a real tool-calling model does.
    /// </summary>
    private sealed class TwoTurnToolChatClient : IChatClient
    {
        private int _callCount;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            _callCount += 1;
            var message =
                _callCount == 1
                    ? new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_weather", new Dictionary<string, object?>())])
                    : new ChatMessage(ChatRole.Assistant, "It is sunny.");
            return Task.FromResult(new ChatResponse(message));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class TelemetryNullProgressSink : IModelCallProgressSink
    {
        public Task OnTextStartAsync(string messageId) => Task.CompletedTask;

        public Task OnTextDeltaAsync(string messageId, string textDelta) => Task.CompletedTask;

        public Task OnTextEndAsync(string messageId) => Task.CompletedTask;

        public Task OnReasoningAsync(string reasoningId, string text) => Task.CompletedTask;

        public Task OnToolCallAsync(string toolCallId, string? toolName, string? argsSnapshot = null, string? parentMessageId = null, string? data = null) => Task.CompletedTask;

        public Task OnToolCallResultAsync(string messageId, string toolCallId, string content) => Task.CompletedTask;

        public Task CompleteAsync() => Task.CompletedTask;

        public Task PersistAsync() => Task.CompletedTask;
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

    private static bool IsOperation(Activity activity, string operationName)
    {
        return activity.GetTagItem("gen_ai.operation.name")?.ToString() == operationName;
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
