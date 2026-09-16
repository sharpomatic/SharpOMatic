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
        Assert.Equal(run.WorkflowId.ToString(), runActivity.GetTagItem("sharpomatic.workflow.id")?.ToString());
        Assert.Equal(workflow.Name, runActivity.GetTagItem("sharpomatic.workflow.name")?.ToString());
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

        // Node identity is what disambiguates two nodes sharing a title, so it must be present and unique.
        Assert.All(nodeActivities, activity => Assert.NotNull(activity.GetTagItem("sharpomatic.executor.id")));
        Assert.All(nodeActivities, activity => Assert.NotNull(activity.GetTagItem("sharpomatic.executor.title")));
        Assert.Equal(
            nodeActivities.Count,
            nodeActivities.Select(activity => activity.GetTagItem("sharpomatic.executor.id")?.ToString()).Distinct().Count()
        );
    }

    [Fact]
    public async Task Engine_owned_span_tags_are_namespaced_to_sharpomatic()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var workflow = new WorkflowBuilder().AddStart().AddEnd().Connect("start", "end").Build();
        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        var runActivity = await WaitForActivity(stopped, activity => activity.OperationName.StartsWith("workflow") && HasRunTag(activity, run.RunId));
        var nodeActivities = stopped.Where(activity => activity.OperationName.StartsWith("executor.process") && HasRunTag(activity, run.RunId)).ToList();
        Assert.NotEmpty(nodeActivities);

        // Anything the engine defines itself must sit under the sharpomatic prefix so it cannot collide
        // with, or be silently absorbed into, the gen_ai and other OpenTelemetry semantic conventions.
        // The only unprefixed tags allowed are the conventions the engine deliberately participates in.
        string[] allowedConventionTags = ["gen_ai.conversation.id", "session.id", "error.type"];

        foreach (var activity in nodeActivities.Append(runActivity))
        {
            foreach (var tag in activity.TagObjects)
            {
                if (allowedConventionTags.Contains(tag.Key))
                    continue;

                Assert.StartsWith("sharpomatic.", tag.Key);
            }
        }
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

    [Fact]
    public async Task Model_call_agent_activity_carries_the_node_id_so_duplicate_titles_stay_distinct()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var caller = new TelemetryTestModelCaller();
        var services = new ServiceCollection()
            .AddSingleton<IOptions<SharpOMaticTelemetryOptions>>(new OptionsWrapper<SharpOMaticTelemetryOptions>(new SharpOMaticTelemetryOptions()))
            .BuildServiceProvider();

        // Two nodes deliberately share a title, which the editor allows: only the id tells them apart.
        var first = caller.InvokeBuildAgentOptions(Guid.NewGuid(), "Ask model");
        var second = caller.InvokeBuildAgentOptions(Guid.NewGuid(), "Ask model");

        foreach (var options in new[] { first, second })
        {
            var agent = caller.InvokeApplyAgentTelemetry(
                new ChatClientAgent(caller.InvokeCreateFunctionInvokingChatClient(new SingleTurnChatClient(), services), options, services: services),
                services
            );
            await caller.InvokeCallConfiguredAgentWithOptions(agent, new ChatOptions());
        }

        var agentActivities = stopped.Where(activity => IsOperation(activity, "invoke_agent")).ToList();
        Assert.Equal(2, agentActivities.Count);

        // The name alone collides, so the id is what a backend can group by without merging the two nodes.
        Assert.All(agentActivities, activity => Assert.Equal("Ask model", activity.GetTagItem("gen_ai.agent.name")?.ToString()));
        Assert.Equal(2, agentActivities.Select(activity => activity.GetTagItem("gen_ai.agent.id")?.ToString()).Distinct().Count());
        Assert.Contains(first.Id, agentActivities.Select(activity => activity.GetTagItem("gen_ai.agent.id")?.ToString()));
        Assert.Contains(second.Id, agentActivities.Select(activity => activity.GetTagItem("gen_ai.agent.id")?.ToString()));
    }

    [Fact]
    public async Task Agent_level_options_survive_the_per_run_options()
    {
        var services = new ServiceCollection()
            .AddSingleton<IOptions<SharpOMaticTelemetryOptions>>(new OptionsWrapper<SharpOMaticTelemetryOptions>(new SharpOMaticTelemetryOptions()))
            .BuildServiceProvider();

        var caller = new TelemetryTestModelCaller();
        var chatClient = new CapturingChatClient();

        // The Anthropic overload has no model parameter, so the model and instructions ride on the agent's
        // own ChatOptions. A per-run ChatOptions must merge with those rather than replace them, or the
        // model call would silently lose the model it was configured with.
        var options = caller.InvokeBuildAgentOptions(Guid.NewGuid(), "Ask model", "the-instructions", "the-model");
        var agent = new ChatClientAgent(caller.InvokeCreateFunctionInvokingChatClient(chatClient, services), options, services: services);

        await caller.InvokeCallConfiguredAgentWithOptions(agent, new ChatOptions { Temperature = 0.5f });

        Assert.Equal("the-model", chatClient.SeenModelId);
        Assert.Equal("the-instructions", chatClient.SeenInstructions);
        Assert.Equal(0.5f, chatClient.SeenTemperature);
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public string? SeenModelId;
        public string? SeenInstructions;
        public float? SeenTemperature;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            SeenModelId = options?.ModelId;
            SeenInstructions = options?.Instructions;
            SeenTemperature = options?.Temperature;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class SingleTurnChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    [Fact]
    public async Task Agent_activity_gives_up_its_usage_so_tokens_are_counted_once()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = CreateListener(stopped);

        var caller = new TelemetryTestModelCaller();
        var services = new ServiceCollection()
            .AddSingleton<IOptions<SharpOMaticTelemetryOptions>>(new OptionsWrapper<SharpOMaticTelemetryOptions>(new SharpOMaticTelemetryOptions()))
            .BuildServiceProvider();

        var options = caller.InvokeBuildAgentOptions(Guid.NewGuid(), "Ask model", "instr", "the-model");
        var agent = caller.InvokeApplyAgentTelemetry(
            new ChatClientAgent(caller.InvokeCreateFunctionInvokingChatClient(new UsageReportingToolChatClient(), services), options, services: services),
            services
        );

        await caller.InvokeCallConfiguredAgent(agent, [AIFunctionFactory.Create(() => "sunny", "get_weather")]);

        // The chat activities are the billing truth: one per provider round trip, each carrying a model.
        var chatActivities = stopped.Where(activity => IsOperation(activity, "chat")).ToList();
        Assert.Equal(2, chatActivities.Count);
        Assert.All(chatActivities, activity => Assert.NotNull(activity.GetTagItem("gen_ai.usage.input_tokens")));
        Assert.All(chatActivities, activity => Assert.Equal("the-model", activity.GetTagItem("gen_ai.request.model")?.ToString()));

        // The agent activity spans the same round trips, so counting its usage as well would double it.
        var agentActivity = await WaitForActivity(stopped, activity => IsOperation(activity, "invoke_agent"));
        Assert.Null(agentActivity.GetTagItem("gen_ai.usage.input_tokens"));
        Assert.Null(agentActivity.GetTagItem("gen_ai.usage.output_tokens"));
        Assert.Null(agentActivity.GetTagItem("gen_ai.usage.total_tokens"));

        // Everything that makes the agent activity worth keeping must survive.
        Assert.Equal("Ask model", agentActivity.GetTagItem("gen_ai.agent.name")?.ToString());
        Assert.Equal(options.Id, agentActivity.GetTagItem("gen_ai.agent.id")?.ToString());

        // Tool spans arrive on the engine's own source, so a host sees them without registering the
        // function invocation middleware's own source - which would bring duplicate usage with it.
        var toolActivity = Assert.Single(stopped, activity => activity.OperationName.StartsWith("execute_tool"));
        Assert.Equal(SharpOMaticDiagnostics.SourceName, toolActivity.Source.Name);
    }

    private sealed class UsageReportingToolChatClient : IChatClient
    {
        private int _callCount;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            _callCount += 1;
            var message =
                _callCount == 1
                    ? new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_weather", new Dictionary<string, object?>())])
                    : new ChatMessage(ChatRole.Assistant, "It is sunny.");
            return Task.FromResult(new ChatResponse(message) { Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20 } });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class TelemetryTestModelCaller : BaseModelCaller
    {
        public AIAgent InvokeApplyAgentTelemetry(AIAgent agent, IServiceProvider serviceProvider) => ApplyAgentTelemetry(agent, serviceProvider);

        public IChatClient InvokeCreateFunctionInvokingChatClient(IChatClient chatClient, IServiceProvider serviceProvider) => CreateFunctionInvokingChatClient(chatClient, serviceProvider);

        public ChatClientAgentOptions InvokeBuildAgentOptions(Guid nodeId, string title, string? instructions = null, string? modelId = null) =>
            BuildAgentOptions(BuildNode(nodeId, title), instructions, modelId);

        public Task<ModelCallResult> InvokeCallConfiguredAgentWithOptions(AIAgent agent, ChatOptions runOptions) =>
            CallConfiguredAgent(agent, [], runOptions, jsonOutput: false, BuildNode(Guid.NewGuid(), "Ask model"), new TelemetryNullProgressSink());

        private static ModelCallNodeEntity BuildNode(Guid nodeId, string title) =>
            new()
            {
                Id = nodeId,
                Version = 1,
                NodeType = NodeType.ModelCall,
                Title = title,
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
