using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallOutputModeUnitTests
{
    [Fact]
    public void Workflow_deserialization_defaults_model_call_batch_output_to_false_when_missing()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var json = SharpOMatic.Engine.Helpers.WorkflowSnapshotSerializer.SerializeWorkflow(workflow);
        json = json.Replace("\"batchOutput\":false,", string.Empty, StringComparison.Ordinal);

        var deserialized = SharpOMatic.Engine.Helpers.WorkflowSnapshotSerializer.DeserializeWorkflow(json);
        var modelNode = Assert.IsType<ModelCallNodeEntity>(deserialized.Nodes.Single(n => n.Title == "model"));
        Assert.False(modelNode.BatchOutput);
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

    private sealed class RoutingTestModelCaller : BaseModelCaller
    {
        public int BatchCallCount { get; private set; }
        public int StreamingCallCount { get; private set; }

        public async Task InvokeConfiguredAgent(ModelCallNodeEntity node)
        {
            await CallConfiguredAgent(
                null!,
                new List<ChatMessage>(),
                chatOptions: null,
                jsonOutput: false,
                node,
                new NullProgressSink()
            );
        }

        public override Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> Call(
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

        protected override Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> CallAgent(
            AIAgent agent,
            List<ChatMessage> chat,
            ChatOptions? chatOptions,
            bool jsonOutput,
            ModelCallNodeEntity node
        )
        {
            BatchCallCount += 1;
            return Task.FromResult<(IList<ChatMessage>, IList<ChatMessage>, ContextObject)>(
                (chat, new List<ChatMessage>(), new ContextObject())
            );
        }

        protected override Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> CallStreamingAgent(
            AIAgent agent,
            List<ChatMessage> chat,
            ChatOptions? chatOptions,
            bool jsonOutput,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            StreamingCallCount += 1;
            return Task.FromResult<(IList<ChatMessage>, IList<ChatMessage>, ContextObject)>(
                (chat, new List<ChatMessage>(), new ContextObject())
            );
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
