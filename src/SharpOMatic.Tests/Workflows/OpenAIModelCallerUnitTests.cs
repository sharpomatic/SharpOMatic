using OpenAI.Responses;

namespace SharpOMatic.Tests.Workflows;

#pragma warning disable OPENAI001

public sealed class OpenAIModelCallerUnitTests
{
    [Fact]
    public void Response_seed_factory_returns_a_new_instance_per_call()
    {
        var caller = new TestableOpenAICaller();

        var options = caller.SetupResponses(CreateModel(), CreateReasoningModelConfig(), CreateNode(("reasoning_effort", "High")));
        var first = RequireSeed(options);
        var second = RequireSeed(options);

        // The chat client requests a seed per provider round trip and adds the converted messages, tools and
        // instructions to it, so sharing one instance across a tool loop sends N copies of the whole request
        // on the Nth round trip.
        Assert.NotSame(first, second);
        Assert.NotSame(first.InputItems, second.InputItems);
    }

    [Fact]
    public void Response_seed_factory_applies_reasoning_effort_to_every_instance()
    {
        var caller = new TestableOpenAICaller();

        var options = caller.SetupResponses(CreateModel(), CreateReasoningModelConfig(), CreateNode(("reasoning_effort", "High")));
        RequireSeed(options);
        var second = RequireSeed(options);

        Assert.Equal("high", second.ReasoningOptions!.ReasoningEffortLevel.ToString());
    }

    private static CreateResponseOptions RequireSeed(ChatOptions options)
    {
        var seed = options.RawRepresentationFactory?.Invoke(null!) as CreateResponseOptions;
        Assert.NotNull(seed);
        return seed;
    }

    private sealed class TestableOpenAICaller : OpenAIModelCaller
    {
        public ChatOptions SetupResponses(Model model, ModelConfig modelConfig, ModelCallNodeEntity node)
        {
            return SetupResponsesBasicCapabilities(model, modelConfig, null!, null!, node).Item1;
        }
    }

    private static Model CreateModel(params (string Key, string Value)[] parameters)
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
            ParameterValues = parameters.ToDictionary(parameter => parameter.Key, parameter => (string?)parameter.Value),
        };
    }

    private static ModelConfig CreateReasoningModelConfig()
    {
        return new ModelConfig
        {
            Version = 1,
            ConfigId = "test-model",
            DisplayName = "gpt-5.4",
            Description = "",
            ConnectorConfigId = "test-connector",
            IsCustom = false,
            Capabilities = [new ModelCapability { Name = "SupportsReasoningEffort", DisplayName = "Supports Reasoning Effort" }],
            ParameterFields =
            [
                new FieldDescriptor
                {
                    Name = "reasoning_effort",
                    Label = "Reasoning Effort",
                    Description = "",
                    CallDefined = true,
                    Type = FieldDescriptorType.Enum,
                    IsRequired = false,
                    Capability = "SupportsReasoningEffort",
                    EnumOptions = ["Low", "Medium", "High"],
                },
            ],
        };
    }

    private static ModelCallNodeEntity CreateNode(params (string Key, string Value)[] parameters)
    {
        return new ModelCallNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.ModelCall,
            Title = "modelcall",
            Top = 0f,
            Left = 0f,
            Width = 0f,
            Height = 0f,
            Inputs = [],
            Outputs = [],
            ModelId = null,
            Instructions = string.Empty,
            Prompt = string.Empty,
            ChatInputPath = string.Empty,
            ChatOutputPath = string.Empty,
            TextOutputPath = string.Empty,
            ImageInputPath = string.Empty,
            ImageOutputPath = string.Empty,
            ParameterValues = parameters.ToDictionary(parameter => parameter.Key, parameter => (string?)parameter.Value),
        };
    }
}
