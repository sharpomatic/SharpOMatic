namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallToolCallingUnitTests
{
    [Fact]
    public void Setup_tool_calling_ignores_selected_tools_that_are_not_registered()
    {
        using var provider = WorkflowRunner.BuildProvider(
            services => services.AddSingleton<IToolMethodRegistry>(new ToolMethodRegistry([(Func<string>)DefinedTool]))
        );
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

        caller.InvokeSetupToolCalling(
            chatOptions,
            CreateToolCallingModel(),
            CreateToolCallingModelConfig(),
            processContext,
            threadContext,
            CreateModelCallNode("MissingTool, DefinedTool")
        );

        Assert.Single(chatOptions.Tools ?? []);
    }

    private static string DefinedTool() => "ok";

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
            ParameterValues = new Dictionary<string, string?>
            {
                ["selected_tools"] = selectedTools,
                ["tool_choice"] = "Auto",
            },
        };
    }

    private sealed class ToolCallingTestModelCaller : BaseModelCaller
    {
        public IServiceProvider InvokeSetupToolCalling(
            ChatOptions chatOptions,
            Model model,
            ModelConfig modelConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node)
        {
            return SetupToolCalling(chatOptions, model, modelConfig, processContext, threadContext, node);
        }

        public override Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink)
        {
            throw new NotSupportedException();
        }
    }
}
