namespace SharpOMatic.Tests.Services;

public sealed class AnthropicMetadataUnitTests
{
    [Fact]
    public void Anthropic_connector_configs_are_embedded()
    {
        var connectorConfigs = LoadEmbeddedMetadata<ConnectorConfig>("Metadata.Resources.ConnectorConfig");

        var direct = Assert.Single(connectorConfigs, config => config.ConfigId == "anthropic");
        Assert.Equal("Anthropic", direct.DisplayName);
        var directAuth = Assert.Single(direct.AuthModes);
        Assert.Equal("api_key", directAuth.Id);
        Assert.Equal(FieldDescriptorType.Secret, Assert.Single(directAuth.Fields).Type);

        var foundry = Assert.Single(connectorConfigs, config => config.ConfigId == "azure_anthropic_foundry");
        Assert.Equal("AI Foundry Anthropic", foundry.DisplayName);
        Assert.Contains(foundry.AuthModes, mode => mode.Id == "api_key" && mode.Fields.Any(field => field.Name == "resource_name") && mode.Fields.Any(field => field.Name == "api_key"));
        Assert.Contains(foundry.AuthModes, mode => mode.Id == "default_azure_credential" && mode.Fields.Single().Name == "resource_name");
    }

    [Fact]
    public void Anthropic_model_configs_are_embedded()
    {
        var modelConfigs = LoadEmbeddedMetadata<ModelConfig>("Metadata.Resources.ModelConfig");

        var directAnthropicConfigs = modelConfigs.Where(config => config.ConnectorConfigId == "anthropic").ToList();
        var foundryAnthropicConfigs = modelConfigs.Where(config => config.ConnectorConfigId == "azure_anthropic_foundry").ToList();
        Assert.Equal(4, directAnthropicConfigs.Count);
        Assert.Equal(4, foundryAnthropicConfigs.Count);

        var expectations = new[]
        {
            new AnthropicModelExpectation("claude-haiku-4-5", ContextWindow: 200_000, MaxOutputTokens: 64_000, SupportsReasoning: false, SupportsStructuredOutput: true),
            new AnthropicModelExpectation("claude-opus-4-8", ContextWindow: 1_000_000, MaxOutputTokens: 128_000, SupportsReasoning: true, SupportsStructuredOutput: true),
            new AnthropicModelExpectation("claude-sonnet-4-6", ContextWindow: 1_000_000, MaxOutputTokens: 128_000, SupportsReasoning: true, SupportsStructuredOutput: false),
            new AnthropicModelExpectation("claude-sonnet-5", ContextWindow: 1_000_000, MaxOutputTokens: 128_000, SupportsReasoning: true, SupportsStructuredOutput: true),
        };

        foreach (var expectation in expectations)
        {
            AssertModelConfig(modelConfigs, $"anthropic_{expectation.DisplayName}", "anthropic", expectation, hasDeploymentName: false);
            AssertModelConfig(modelConfigs, $"azure_anthropic_foundry_{expectation.DisplayName}", "azure_anthropic_foundry", expectation, hasDeploymentName: true);
        }
    }

    private static void AssertModelConfig(List<ModelConfig> modelConfigs, string configId, string connectorConfigId, AnthropicModelExpectation expectation, bool hasDeploymentName)
    {
        var config = Assert.Single(modelConfigs, config => config.ConfigId == configId);
        Assert.False(config.IsCustom);
        Assert.Equal(connectorConfigId, config.ConnectorConfigId);
        Assert.Equal(expectation.DisplayName, config.DisplayName);
        Assert.Contains(config.Capabilities, capability => capability.Name == "SupportsTextIn");
        Assert.Contains(config.Capabilities, capability => capability.Name == "SupportsToolCalling");
        AssertCapability(config, "SupportsReasoningEffort", expectation.SupportsReasoning);
        AssertCapability(config, "SupportsStructuredOutput", expectation.SupportsStructuredOutput);
        Assert.Contains(config.ParameterFields, field => field.Name == "parallel_tool_calls");
        Assert.DoesNotContain(config.ParameterFields, field => field.Name == "top_k");
        Assert.Equal(expectation.MaxOutputTokens, GetFieldDefaultValue(config, "max_output_tokens"));
        Assert.Equal(expectation.ContextWindow, GetInformationValue(config, "ContextWindow"));

        if (hasDeploymentName)
            Assert.Contains(config.ParameterFields, field => field.Name == "deployment_name" && !field.CallDefined && field.IsRequired);
        else
            Assert.DoesNotContain(config.ParameterFields, field => field.Name == "deployment_name");
    }

    private static void AssertCapability(ModelConfig config, string capabilityName, bool expected)
    {
        var hasCapability = config.Capabilities.Any(capability => capability.Name == capabilityName);
        Assert.Equal(expected, hasCapability);

        if (capabilityName == "SupportsReasoningEffort")
            Assert.Equal(expected, config.ParameterFields.Any(field => field.Name == "thinking_mode"));

        if (capabilityName == "SupportsStructuredOutput")
            Assert.Equal(expected, config.ParameterFields.Any(field => field.Name == "structured_output"));
    }

    private static int GetFieldDefaultValue(ModelConfig config, string fieldName)
    {
        var value = config.ParameterFields.Single(field => field.Name == fieldName).DefaultValue;
        return value switch
        {
            int intValue => intValue,
            JsonElement element => element.GetInt32(),
            _ => throw new InvalidOperationException($"Unexpected value for field '{fieldName}'."),
        };
    }

    private static int GetInformationValue(ModelConfig config, string informationName)
    {
        var value = config.Information?.Single(item => item.Name == informationName).Value;
        return value switch
        {
            int intValue => intValue,
            JsonElement element => element.GetInt32(),
            _ => throw new InvalidOperationException($"Unexpected value for information '{informationName}'."),
        };
    }

    private static List<T> LoadEmbeddedMetadata<T>(string resourceFilter)
    {
        var assembly = typeof(ModelConfig).Assembly;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var results = new List<T>();

        foreach (var resourceName in assembly.GetManifestResourceNames().Where(name => name.Contains(resourceFilter, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            Assert.NotNull(stream);
            var metadata = JsonSerializer.Deserialize<T>(stream, options);
            Assert.NotNull(metadata);
            results.Add(metadata);
        }

        return results;
    }

    private sealed record AnthropicModelExpectation(string DisplayName, int ContextWindow, int MaxOutputTokens, bool SupportsReasoning, bool SupportsStructuredOutput);
}
