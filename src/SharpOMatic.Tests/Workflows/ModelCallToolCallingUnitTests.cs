namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallToolCallingUnitTests
{
    [Fact]
    public void Tool_registry_uses_display_name_attribute_for_tool_names()
    {
        var registry = new ToolMethodRegistry([(Func<string>)DefinedToolWithDisplayName]);

        var toolName = Assert.Single(registry.GetToolDisplayNames());
        var tool = Assert.IsType<Func<string>>(registry.GetToolFromDisplayName("defined_tool"));

        Assert.Equal("defined_tool", toolName);
        Assert.Equal("ok", tool());
        Assert.Null(registry.GetToolFromDisplayName(nameof(DefinedToolWithDisplayName)));
    }

    [Fact]
    public void Tool_registry_rejects_duplicate_display_names()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ToolMethodRegistry([(Func<string>)DefinedToolWithDisplayName, (Func<string>)DuplicateDefinedToolWithDisplayName]));

        Assert.Contains("defined_tool", exception.Message);
    }

    [Fact]
    public void Setup_tool_calling_ignores_selected_tools_that_are_not_registered()
    {
        using var provider = WorkflowRunner.BuildProvider(services => services.AddSingleton<IToolMethodRegistry>(new ToolMethodRegistry([(Func<string>)DefinedTool])));
        using var scope = provider.CreateScope();

        var workflow = new WorkflowEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            Name = "Tool workflow",
            Description = "",
            Nodes = [],
            Connections = [],
        };
        var run = new Run
        {
            RunId = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Created = DateTime.UtcNow,
            RunStatus = RunStatus.Running,
        };
        var processContext = new ProcessContext(scope, run, 100, null);
        var workflowContext = new WorkflowContext(processContext, workflow);
        var threadContext = new ThreadContext(processContext, workflowContext, []);
        var caller = new ToolCallingTestModelCaller();
        var chatOptions = new ChatOptions { AdditionalProperties = [] };

        caller.InvokeSetupToolCalling(chatOptions, CreateToolCallingModel(), CreateToolCallingModelConfig(), processContext, threadContext, CreateModelCallNode("MissingTool, DefinedTool"));

        Assert.Single(chatOptions.Tools ?? []);
    }

    [Fact]
    public void Setup_tool_calling_uses_display_name_attribute_for_selected_tools()
    {
        using var provider = WorkflowRunner.BuildProvider(services => services.AddSingleton<IToolMethodRegistry>(new ToolMethodRegistry([(Func<string>)DefinedToolWithDisplayName])));
        using var scope = provider.CreateScope();

        var workflow = new WorkflowEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            Name = "Tool workflow",
            Description = "",
            Nodes = [],
            Connections = [],
        };
        var run = new Run
        {
            RunId = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Created = DateTime.UtcNow,
            RunStatus = RunStatus.Running,
        };
        var processContext = new ProcessContext(scope, run, 100, null);
        var workflowContext = new WorkflowContext(processContext, workflow);
        var threadContext = new ThreadContext(processContext, workflowContext, []);
        var caller = new ToolCallingTestModelCaller();
        var chatOptions = new ChatOptions { AdditionalProperties = [] };

        caller.InvokeSetupToolCalling(chatOptions, CreateToolCallingModel(), CreateToolCallingModelConfig(), processContext, threadContext, CreateModelCallNode("defined_tool"));

        Assert.Single(chatOptions.Tools ?? []);
    }

    [Fact]
    public async Task Tool_graceful_stop_exception_ends_streaming_model_call_with_partial_messages()
    {
        var caller = new ToolCallingTestModelCaller();
        var agent = new ChatClientAgent(caller.InvokeCreateFunctionInvokingChatClient(new ToolRequestingChatClient()));

        var result = await caller.InvokeStreamingAgent(agent, CreateModelCallNode("NeedsUserInput"), [AIFunctionFactory.Create((Func<string>)NeedsUserInput, "NeedsUserInput")]);

        Assert.Equal(string.Empty, result.ResultValue);
        var assistantMessage = Assert.Single(result.Responses);
        var functionCall = Assert.IsType<FunctionCallContent>(assistantMessage.Contents.Single());
        Assert.Equal("call-1", functionCall.CallId);
        Assert.Equal("NeedsUserInput", functionCall.Name);
    }

    [Fact]
    public void Graceful_stop_exception_detection_unwraps_common_exception_wrappers()
    {
        var exception = new AggregateException(new InvalidOperationException("outer", new System.Reflection.TargetInvocationException(new ModelCallGracefulStopException("need input"))));

        Assert.True(ToolCallingTestModelCaller.ContainsGracefulStop(exception));
    }

    private static string DefinedTool() => "ok";

    [System.ComponentModel.DisplayName("defined_tool")]
    private static string DefinedToolWithDisplayName() => "ok";

    [System.ComponentModel.DisplayName("defined_tool")]
    private static string DuplicateDefinedToolWithDisplayName() => "duplicate";

    private static string NeedsUserInput()
    {
        throw new ModelCallGracefulStopException("Need more user input.");
    }

    private static Model CreateToolCallingModel()
    {
        return new Model
        {
            ModelId = Guid.NewGuid(),
            Version = 1,
            ConfigId = "test-model",
            ConnectorId = Guid.NewGuid(),
            Name = "Test model",
            Description = "",
            CustomCapabilities = [],
            ParameterValues = [],
        };
    }

    private static ModelConfig CreateToolCallingModelConfig()
    {
        return new ModelConfig
        {
            Version = 1,
            ConfigId = "test-model",
            DisplayName = "Test model",
            Description = "",
            ConnectorConfigId = "test-connector",
            IsCustom = false,
            Capabilities = [new ModelCapability { Name = "SupportsToolCalling", DisplayName = "Supports Tool Calling" }],
            ParameterFields = [],
        };
    }

    private static ModelCallNodeEntity CreateModelCallNode(string selectedTools)
    {
        return new ModelCallNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.ModelCall,
            Title = "Model Call",
            Top = 0,
            Left = 0,
            Width = 80,
            Height = 80,
            Inputs = [],
            Outputs = [],
            ModelId = Guid.NewGuid(),
            Instructions = "",
            Prompt = "",
            ChatInputPath = "",
            ChatOutputPath = "",
            TextOutputPath = "",
            ImageInputPath = "",
            ImageOutputPath = "",
            ParameterValues = new Dictionary<string, string?> { ["selected_tools"] = selectedTools, ["tool_choice"] = "Auto" },
        };
    }

    private sealed class ToolCallingTestModelCaller : BaseModelCaller
    {
        public static bool ContainsGracefulStop(Exception exception)
        {
            return TryGetGracefulStopException(exception, out _);
        }

        public IServiceProvider InvokeSetupToolCalling(
            ChatOptions chatOptions,
            Model model,
            ModelConfig modelConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node
        )
        {
            return SetupToolCalling(chatOptions, model, modelConfig, processContext, threadContext, node);
        }

        public Task<ModelCallResult> InvokeStreamingAgent(AIAgent agent, ModelCallNodeEntity node, IList<AITool> tools)
        {
            return CallStreamingAgent(agent, [], new ChatOptions() { Tools = tools }, jsonOutput: true, node, new NullProgressSink());
        }

        public IChatClient InvokeCreateFunctionInvokingChatClient(IChatClient chatClient)
        {
            return CreateFunctionInvokingChatClient(chatClient, toolServiceProvider: null);
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
    }

    private sealed class ToolRequestingChatClient : IChatClient
    {
        private int _callCount;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(CreateToolCallMessage()));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            _callCount += 1;
            if (_callCount > 1)
                throw new InvalidOperationException("The model call should stop after the tool throws ModelCallGracefulStopException.");

            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "NeedsUserInput", new Dictionary<string, object?>())]) { MessageId = "assistant-1" };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }

        private static ChatMessage CreateToolCallMessage()
        {
            return new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "NeedsUserInput", new Dictionary<string, object?>())]);
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
