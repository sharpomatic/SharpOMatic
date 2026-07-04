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

        // Only the sonnet-4-6 configs exist today; opus-4-8 and custom variants are not built yet.
        AssertModelConfig(modelConfigs, "anthropic_claude-sonnet-4-6", "anthropic", "claude-sonnet-4-6", contextWindow: 1_000_000, maxOutputTokens: 64_000, hasDeploymentName: false);
        AssertModelConfig(modelConfigs, "azure_anthropic_foundry_claude-sonnet-4-6", "azure_anthropic_foundry", "claude-sonnet-4-6", contextWindow: 1_000_000, maxOutputTokens: 64_000, hasDeploymentName: true);
    }

    private static void AssertModelConfig(List<ModelConfig> modelConfigs, string configId, string connectorConfigId, string displayName, int contextWindow, int maxOutputTokens, bool hasDeploymentName)
    {
        var config = Assert.Single(modelConfigs, config => config.ConfigId == configId);
        Assert.False(config.IsCustom);
        Assert.Equal(connectorConfigId, config.ConnectorConfigId);
        Assert.Equal(displayName, config.DisplayName);
        Assert.Contains(config.Capabilities, capability => capability.Name == "SupportsTextIn");
        Assert.Contains(config.Capabilities, capability => capability.Name == "SupportsToolCalling");
        Assert.DoesNotContain(config.Capabilities, capability => capability.Name == "SupportsStructuredOutput");
        Assert.Contains(config.ParameterFields, field => field.Name == "parallel_tool_calls");
        Assert.DoesNotContain(config.ParameterFields, field => field.Name == "top_k");
        Assert.Equal(maxOutputTokens, GetFieldDefaultValue(config, "max_output_tokens"));
        Assert.Equal(contextWindow, GetInformationValue(config, "ContextWindow"));

        if (hasDeploymentName)
            Assert.Contains(config.ParameterFields, field => field.Name == "deployment_name" && !field.CallDefined && field.IsRequired);
        else
            Assert.DoesNotContain(config.ParameterFields, field => field.Name == "deployment_name");
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
}
