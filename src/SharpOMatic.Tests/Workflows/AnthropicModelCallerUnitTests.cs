using Anthropic;
using Anthropic.Foundry;
using AnthropicMessages = Anthropic.Models.Messages;

namespace SharpOMatic.Tests.Workflows;

public sealed class AnthropicModelCallerUnitTests
{
    [Fact]
    public void Engine_registers_anthropic_model_callers()
    {
        var services = new ServiceCollection();
        services.AddSharpOMaticEngine();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<AnthropicModelCaller>(provider.GetRequiredKeyedService<IModelCaller>("anthropic"));
        Assert.IsType<FoundryAnthropicModelCaller>(provider.GetRequiredKeyedService<IModelCaller>("azure_anthropic_foundry"));
    }

    [Fact]
    public void Direct_anthropic_client_uses_builtin_model_name()
    {
        var caller = new AnthropicModelCaller();

        var result = caller.GetAnthropicClient(CreateModel(), CreateModelConfig("claude-sonnet-4-6", isCustom: false), CreateAuthMode("api_key"), new() { ["api_key"] = "key" });

        Assert.IsType<AnthropicClient>(result.client);
        Assert.Equal("claude-sonnet-4-6", result.modelName);
    }

    [Fact]
    public void Direct_anthropic_client_uses_custom_model_name()
    {
        var caller = new AnthropicModelCaller();
        var model = CreateModel(("model_name", "claude-custom"));

        var result = caller.GetAnthropicClient(model, CreateModelConfig("custom", isCustom: true), CreateAuthMode("api_key"), new() { ["api_key"] = "key" });

        Assert.Equal("claude-custom", result.modelName);
    }

    [Fact]
    public void Direct_anthropic_client_requires_api_key()
    {
        var caller = new AnthropicModelCaller();

        var exception = Assert.Throws<SharpOMaticException>(() => caller.GetAnthropicClient(CreateModel(), CreateModelConfig("claude-sonnet-4-6"), CreateAuthMode("api_key"), []));

        Assert.Equal("Connector api key not specified.", exception.Message);
    }

    [Fact]
    public void Direct_anthropic_client_uses_override()
    {
        var overrideClient = new AnthropicClient { ApiKey = "override" };
        var caller = new AnthropicModelCaller([new AnthropicOverrideNotification(overrideClient, "override-model")]);

        var result = caller.GetAnthropicClient(CreateModel(), CreateModelConfig("claude-sonnet-4-6"), CreateAuthMode("api_key"), []);

        Assert.Same(overrideClient, result.client);
        Assert.Equal("override-model", result.modelName);
    }

    [Fact]
    public void Foundry_anthropic_client_uses_api_key_auth_and_deployment_name()
    {
        var caller = new FoundryAnthropicModelCaller();
        var model = CreateModel(("deployment_name", "claude-deployment"));

        var result = caller.GetAnthropicClient(model, CreateModelConfig("claude-sonnet-4-6"), CreateAuthMode("api_key"), new() { ["resource_name"] = "foundry-resource", ["api_key"] = "key" });

        Assert.IsType<AnthropicFoundryClient>(result.client);
        Assert.Equal("claude-deployment", result.modelName);
    }

    [Fact]
    public void Foundry_anthropic_client_uses_default_azure_credential_auth()
    {
        var caller = new FoundryAnthropicModelCaller();
        var model = CreateModel(("deployment_name", "claude-deployment"));

        var result = caller.GetAnthropicClient(model, CreateModelConfig("claude-sonnet-4-6"), CreateAuthMode("default_azure_credential"), new() { ["resource_name"] = "foundry-resource" });

        Assert.IsType<AnthropicFoundryClient>(result.client);
        Assert.Equal("claude-deployment", result.modelName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("resource name")]
    [InlineData("https://foundry-resource.services.ai.azure.com")]
    public void Foundry_anthropic_client_validates_resource_name(string resourceName)
    {
        var caller = new FoundryAnthropicModelCaller();
        var model = CreateModel(("deployment_name", "claude-deployment"));

        var exception = Assert.Throws<SharpOMaticException>(() =>
            caller.GetAnthropicClient(model, CreateModelConfig("claude-sonnet-4-6"), CreateAuthMode("default_azure_credential"), new() { ["resource_name"] = resourceName })
        );

        Assert.Contains("resource name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Foundry_anthropic_client_requires_deployment_name()
    {
        var caller = new FoundryAnthropicModelCaller();

        var exception = Assert.Throws<SharpOMaticException>(() =>
            caller.GetAnthropicClient(CreateModel(), CreateModelConfig("claude-sonnet-4-6"), CreateAuthMode("default_azure_credential"), new() { ["resource_name"] = "foundry-resource" })
        );

        Assert.Equal("Model does not specify a deployment name", exception.Message);
    }

    [Fact]
    public void Foundry_anthropic_client_uses_override()
    {
        var overrideClient = new AnthropicFoundryClient(new AnthropicFoundryApiKeyCredentials("key", "foundry-resource"));
        var caller = new FoundryAnthropicModelCaller([new FoundryAnthropicOverrideNotification(overrideClient, "override-deployment")]);

        var result = caller.GetAnthropicClient(CreateModel(), CreateModelConfig("claude-sonnet-4-6"), CreateAuthMode("api_key"), []);

        Assert.Same(overrideClient, result.client);
        Assert.Equal("override-deployment", result.modelName);
    }

    [Fact]
    public void Reasoning_defaults_to_adaptive_thinking_with_configured_effort()
    {
        var caller = new TestableAnthropicCaller();

        var seed = RequireSeed(caller.SetupReasoning(CreateModel(), CreateReasoningModelConfig(), CreateNode(("effort_level", "High"))));

        Assert.NotNull(seed.OutputConfig);
        Assert.Equal("high", seed.OutputConfig!.Effort!.ToString().Trim('"'));
        Assert.Contains("adaptive", SerializeThinking(seed));
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("XHigh")]
    [InlineData("Max")]
    public void Reasoning_maps_each_effort_level(string level)
    {
        var caller = new TestableAnthropicCaller();

        var seed = RequireSeed(caller.SetupReasoning(CreateModel(), CreateReasoningModelConfig(), CreateNode(("effort_level", level))));

        Assert.NotNull(seed.OutputConfig);
        Assert.Equal(level.ToLowerInvariant(), seed.OutputConfig!.Effort!.ToString().Trim('"'));
    }

    [Fact]
    public void Reasoning_disabled_thinking_mode_disables_thinking()
    {
        var caller = new TestableAnthropicCaller();

        var seed = RequireSeed(caller.SetupReasoning(CreateModel(), CreateReasoningModelConfig(), CreateNode(("thinking_mode", "Disabled"))));

        var thinking = SerializeThinking(seed);
        Assert.Contains("disabled", thinking);
        Assert.DoesNotContain("adaptive", thinking);
    }

    [Fact]
    public void Model_without_reasoning_capability_gets_no_anthropic_seed()
    {
        var caller = new TestableAnthropicCaller();

        var options = caller.SetupReasoning(CreateModel(), CreateModelConfig("claude-sonnet-4-6"), CreateNode());

        Assert.Null(options.RawRepresentationFactory);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Parallel_tool_calls_maps_to_allow_multiple_tool_calls(string value, bool expected)
    {
        var caller = new TestableAnthropicCaller();
        var options = new ChatOptions();

        caller.ApplyParallel(options, CreateModel(), CreateToolCallingModelConfig(), CreateNode(("parallel_tool_calls", value)));

        Assert.Equal(expected, options.AllowMultipleToolCalls);
    }

    [Fact]
    public void Parallel_tool_calls_unset_leaves_allow_multiple_tool_calls_at_model_default()
    {
        var caller = new TestableAnthropicCaller();
        var options = new ChatOptions();

        caller.ApplyParallel(options, CreateModel(), CreateToolCallingModelConfig(), CreateNode());

        Assert.Null(options.AllowMultipleToolCalls);
    }

    private static AnthropicMessages.MessageCreateParams RequireSeed(ChatOptions options)
    {
        var seed = options.RawRepresentationFactory?.Invoke(null!) as AnthropicMessages.MessageCreateParams;
        Assert.NotNull(seed);
        return seed;
    }

    private static string SerializeThinking(AnthropicMessages.MessageCreateParams seed)
    {
        return System.Text.Json.JsonSerializer.Serialize(seed.Thinking);
    }

    private static ModelConfig CreateReasoningModelConfig()
    {
        return new ModelConfig
        {
            Version = 1,
            ConfigId = "test-model",
            DisplayName = "claude-sonnet-4-6",
            Description = "",
            ConnectorConfigId = "test-connector",
            IsCustom = false,
            Capabilities = [new ModelCapability { Name = "SupportsReasoningEffort", DisplayName = "Supports Reasoning Effort" }],
            ParameterFields =
            [
                new FieldDescriptor
                {
                    Name = "thinking_mode",
                    Label = "Thinking",
                    Description = "",
                    CallDefined = true,
                    Type = FieldDescriptorType.Enum,
                    IsRequired = false,
                    Capability = "SupportsReasoningEffort",
                    EnumOptions = ["Adaptive", "Disabled"],
                },
                new FieldDescriptor
                {
                    Name = "effort_level",
                    Label = "Effort Level",
                    Description = "",
                    CallDefined = true,
                    Type = FieldDescriptorType.Enum,
                    IsRequired = false,
                    Capability = "SupportsReasoningEffort",
                    EnumOptions = ["Low", "Medium", "High", "Max"],
                },
            ],
        };
    }

    private static ModelConfig CreateToolCallingModelConfig()
    {
        return new ModelConfig
        {
            Version = 1,
            ConfigId = "test-model",
            DisplayName = "claude-sonnet-4-6",
            Description = "",
            ConnectorConfigId = "test-connector",
            IsCustom = false,
            Capabilities = [new ModelCapability { Name = "SupportsToolCalling", DisplayName = "Supports Tool Calling" }],
            ParameterFields =
            [
                new FieldDescriptor
                {
                    Name = "parallel_tool_calls",
                    Label = "Parallel Tool Calls",
                    Description = "",
                    CallDefined = true,
                    Type = FieldDescriptorType.Boolean,
                    IsRequired = false,
                    Capability = "SupportsToolCalling",
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

    private sealed class TestableAnthropicCaller : AnthropicModelCaller
    {
        public ChatOptions SetupReasoning(Model model, ModelConfig modelConfig, ModelCallNodeEntity node)
        {
            return SetupAnthropicBasicCapabilities(model, modelConfig, null!, null!, node);
        }

        public void ApplyParallel(ChatOptions chatOptions, Model model, ModelConfig modelConfig, ModelCallNodeEntity node)
        {
            ApplyParallelToolCalls(chatOptions, model, modelConfig, node);
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

    private static ModelConfig CreateModelConfig(string displayName, bool isCustom = false)
    {
        return new ModelConfig
        {
            Version = 1,
            ConfigId = "test-model",
            DisplayName = displayName,
            Description = "",
            ConnectorConfigId = "test-connector",
            IsCustom = isCustom,
            Capabilities = [],
            ParameterFields = [],
        };
    }

    private static AuthenticationModeConfig CreateAuthMode(string id)
    {
        return new AuthenticationModeConfig
        {
            Id = id,
            DisplayName = id,
            Kind = AuthenticationModeKind.ApiKey,
            IsDefault = true,
            Fields = [],
        };
    }

    private sealed class AnthropicOverrideNotification(AnthropicClient client, string modelName) : IEngineNotification
    {
        public (AnthropicClient client, string modelName)? AnthropicOverride(
            Model model,
            ModelConfig modelConfig,
            AuthenticationModeConfig authenticationModeConfig,
            Dictionary<string, string?> connectionFields
        )
        {
            return (client, modelName);
        }
    }

    private sealed class FoundryAnthropicOverrideNotification(AnthropicClient client, string modelName) : IEngineNotification
    {
        public (AnthropicClient client, string modelName)? FoundryAnthropicOverride(
            Model model,
            ModelConfig modelConfig,
            AuthenticationModeConfig authenticationModeConfig,
            Dictionary<string, string?> connectionFields
        )
        {
            return (client, modelName);
        }
    }
}
