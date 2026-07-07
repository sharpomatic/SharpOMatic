namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallOutputModeUnitTests
{
    [Fact]
    public void Workflow_deserialization_defaults_model_call_batch_output_to_false_when_missing()
    {
        var workflow = new WorkflowBuilder().AddStart().AddModelCall("model").AddEnd().Connect("start", "model").Connect("model", "end").Build();

        var json = SharpOMatic.Engine.Helpers.WorkflowSnapshotSerializer.SerializeWorkflow(workflow);
        json = json.Replace("\"batchOutput\":false,", string.Empty, StringComparison.Ordinal);
        json = json.Replace("\"dropToolCalls\":false,", string.Empty, StringComparison.Ordinal);
        json = json.Replace("\"disableStreamUser\":false,", string.Empty, StringComparison.Ordinal);

        var deserialized = SharpOMatic.Engine.Helpers.WorkflowSnapshotSerializer.DeserializeWorkflow(json);
        var modelNode = Assert.IsType<ModelCallNodeEntity>(deserialized.Nodes.Single(n => n.Title == "model"));
        Assert.False(modelNode.BatchOutput);
        Assert.False(modelNode.DropToolCalls);
        Assert.False(modelNode.DisableStreamUser);
        Assert.False(modelNode.DisableStreamTool);
        Assert.False(modelNode.DisableStreamReasoning);
        Assert.False(modelNode.DisableStreamAssistantText);
    }

    [Fact]
    public async Task Base_model_caller_uses_streaming_output_when_batch_output_is_false()
    {
        var caller = new RoutingTestModelCaller();
        var node = new ModelCallNodeEntity()
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.ModelCall,
            Title = "model",
            Top = 0,
            Left = 0,
            Width = 80,
            Height = 80,
            Inputs = [],
            Outputs = [],
            ModelId = null,
            BatchOutput = false,
            DropToolCalls = false,
            DisableStreamUser = false,
            DisableStreamTool = false,
            DisableStreamReasoning = false,
            DisableStreamAssistantText = false,
            Instructions = string.Empty,
            Prompt = string.Empty,
            ChatInputPath = string.Empty,
            ChatOutputPath = string.Empty,
            TextOutputPath = "output.text",
            ImageInputPath = string.Empty,
            ImageOutputPath = "output.image",
            ParameterValues = [],
        };

        await caller.InvokeConfiguredAgent(node);

        Assert.Equal(0, caller.BatchCallCount);
        Assert.Equal(1, caller.StreamingCallCount);
    }

    [Fact]
    public async Task Base_model_caller_uses_batch_output_when_batch_output_is_true()
    {
        var caller = new RoutingTestModelCaller();
        var node = new ModelCallNodeEntity()
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.ModelCall,
            Title = "model",
            Top = 0,
            Left = 0,
            Width = 80,
            Height = 80,
            Inputs = [],
            Outputs = [],
            ModelId = null,
            BatchOutput = true,
            DropToolCalls = false,
            DisableStreamUser = false,
            DisableStreamTool = false,
            DisableStreamReasoning = false,
            DisableStreamAssistantText = false,
            Instructions = string.Empty,
            Prompt = string.Empty,
            ChatInputPath = string.Empty,
            ChatOutputPath = string.Empty,
            TextOutputPath = "output.text",
            ImageInputPath = string.Empty,
            ImageOutputPath = "output.image",
            ParameterValues = [],
        };

        await caller.InvokeConfiguredAgent(node);

        Assert.Equal(1, caller.BatchCallCount);
        Assert.Equal(0, caller.StreamingCallCount);
    }

    [Theory]
    [InlineData(false, false, false, false, false)] // nothing disabled -> not suppressed
    [InlineData(true, false, false, false, false)] // only assistant text disabled
    [InlineData(true, true, false, false, false)] // assistant text + reasoning, tool still on
    [InlineData(true, true, true, false, true)] // all three response streams disabled -> suppressed
    [InlineData(true, true, true, true, true)] // DisableStreamUser does not change the result
    public void Is_ag_ui_response_stream_suppressed_reflects_the_three_response_flags_ignoring_user(
        bool disableStreamAssistantText,
        bool disableStreamReasoning,
        bool disableStreamTool,
        bool disableStreamUser,
        bool expected
    )
    {
        var node = CreateRoutingNode(
            batchOutput: false,
            disableStreamTool: disableStreamTool,
            disableStreamReasoning: disableStreamReasoning,
            disableStreamAssistantText: disableStreamAssistantText,
            disableStreamUser: disableStreamUser
        );

        Assert.Equal(expected, node.IsAgUiResponseStreamSuppressed());
    }

    [Fact]
    public void Is_ag_ui_response_stream_suppressed_is_false_when_any_tool_is_always()
    {
        var node = CreateRoutingNode(batchOutput: false, disableStreamTool: true, disableStreamReasoning: true, disableStreamAssistantText: true);
        node.ToolAgUiOutputModes["a"] = ModelCallToolAgUiOutputMode.Never;
        node.ToolAgUiOutputModes["b"] = ModelCallToolAgUiOutputMode.Always;

        Assert.False(node.IsAgUiResponseStreamSuppressed());
    }

    [Fact]
    public void Is_ag_ui_response_stream_suppressed_is_true_when_tools_are_only_never_or_inherit()
    {
        var node = CreateRoutingNode(batchOutput: false, disableStreamTool: true, disableStreamReasoning: true, disableStreamAssistantText: true);
        node.ToolAgUiOutputModes["a"] = ModelCallToolAgUiOutputMode.Never;
        node.ToolAgUiOutputModes["b"] = ModelCallToolAgUiOutputMode.Inherit;

        Assert.True(node.IsAgUiResponseStreamSuppressed());
    }

    [Fact]
    public async Task Base_model_caller_forces_batch_output_when_all_response_stream_events_are_disabled()
    {
        var caller = new RoutingTestModelCaller();
        var node = CreateRoutingNode(batchOutput: false, disableStreamTool: true, disableStreamReasoning: true, disableStreamAssistantText: true);

        await caller.InvokeConfiguredAgent(node);

        // Nothing can stream incrementally, so streaming from the provider is pointless: force the batch path.
        Assert.Equal(1, caller.BatchCallCount);
        Assert.Equal(0, caller.StreamingCallCount);
    }

    [Fact]
    public async Task Base_model_caller_still_streams_when_a_tool_is_set_to_always_despite_disabled_stream_events()
    {
        var caller = new RoutingTestModelCaller();
        var node = CreateRoutingNode(batchOutput: false, disableStreamTool: true, disableStreamReasoning: true, disableStreamAssistantText: true);
        node.ToolAgUiOutputModes["lookup_weather"] = ModelCallToolAgUiOutputMode.Always;

        await caller.InvokeConfiguredAgent(node);

        // A per-tool Always override re-enables tool events, so the response can still emit incrementally.
        Assert.Equal(0, caller.BatchCallCount);
        Assert.Equal(1, caller.StreamingCallCount);
    }

    [Fact]
    public async Task Base_model_caller_forces_batch_output_when_tool_is_set_to_never_and_stream_events_disabled()
    {
        var caller = new RoutingTestModelCaller();
        var node = CreateRoutingNode(batchOutput: false, disableStreamTool: true, disableStreamReasoning: true, disableStreamAssistantText: true);
        node.ToolAgUiOutputModes["lookup_weather"] = ModelCallToolAgUiOutputMode.Never;

        await caller.InvokeConfiguredAgent(node);

        // Never only makes things more suppressed, so it does not prevent the batch optimisation.
        Assert.Equal(1, caller.BatchCallCount);
        Assert.Equal(0, caller.StreamingCallCount);
    }

    [Fact]
    public async Task Base_model_caller_streams_when_only_some_response_stream_events_are_disabled()
    {
        var caller = new RoutingTestModelCaller();
        var node = CreateRoutingNode(batchOutput: false, disableStreamTool: false, disableStreamReasoning: true, disableStreamAssistantText: true);

        await caller.InvokeConfiguredAgent(node);

        // Tool events are still enabled, so we must keep streaming to deliver them incrementally.
        Assert.Equal(0, caller.BatchCallCount);
        Assert.Equal(1, caller.StreamingCallCount);
    }

    [Fact]
    public async Task Base_model_caller_uses_batch_output_when_batch_output_is_true_even_with_always_tool()
    {
        var caller = new RoutingTestModelCaller();
        var node = CreateRoutingNode(batchOutput: true, disableStreamTool: false, disableStreamReasoning: false, disableStreamAssistantText: false);
        node.ToolAgUiOutputModes["lookup_weather"] = ModelCallToolAgUiOutputMode.Always;

        await caller.InvokeConfiguredAgent(node);

        // Explicit batch output always wins regardless of tool overrides.
        Assert.Equal(1, caller.BatchCallCount);
        Assert.Equal(0, caller.StreamingCallCount);
    }

    private static ModelCallNodeEntity CreateRoutingNode(
        bool batchOutput,
        bool disableStreamTool = false,
        bool disableStreamReasoning = false,
        bool disableStreamAssistantText = false,
        bool disableStreamUser = false
    )
    {
        return new ModelCallNodeEntity()
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.ModelCall,
            Title = "model",
            Top = 0,
            Left = 0,
            Width = 80,
            Height = 80,
            Inputs = [],
            Outputs = [],
            ModelId = null,
            BatchOutput = batchOutput,
            DropToolCalls = false,
            DisableStreamUser = disableStreamUser,
            DisableStreamTool = disableStreamTool,
            DisableStreamReasoning = disableStreamReasoning,
            DisableStreamAssistantText = disableStreamAssistantText,
            Instructions = string.Empty,
            Prompt = string.Empty,
            ChatInputPath = string.Empty,
            ChatOutputPath = string.Empty,
            TextOutputPath = "output.text",
            ImageInputPath = string.Empty,
            ImageOutputPath = "output.image",
            ParameterValues = [],
        };
    }

    private sealed class RoutingTestModelCaller : BaseModelCaller
    {
        public int BatchCallCount { get; private set; }
        public int StreamingCallCount { get; private set; }

        public async Task InvokeConfiguredAgent(ModelCallNodeEntity node)
        {
            await CallConfiguredAgent(null!, new List<ChatMessage>(), chatOptions: null, jsonOutput: false, node, new NullProgressSink());
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
        )
        {
            throw new NotSupportedException();
        }

        protected override Task<ModelCallResult> CallAgent(AIAgent agent, List<ChatMessage> chat, ChatOptions? chatOptions, bool jsonOutput, ModelCallNodeEntity node)
        {
            BatchCallCount += 1;
            return Task.FromResult<ModelCallResult>((chat, new List<ChatMessage>(), string.Empty));
        }

        protected override Task<ModelCallResult> CallStreamingAgent(
            AIAgent agent,
            List<ChatMessage> chat,
            ChatOptions? chatOptions,
            bool jsonOutput,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            StreamingCallCount += 1;
            return Task.FromResult<ModelCallResult>((chat, new List<ChatMessage>(), string.Empty));
        }
    }

    private sealed class NullProgressSink : IModelCallProgressSink
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
}
