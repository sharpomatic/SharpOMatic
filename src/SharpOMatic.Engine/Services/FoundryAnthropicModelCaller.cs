namespace SharpOMatic.Engine.Services;

public class FoundryAnthropicModelCaller(IEnumerable<IEngineNotification> engineNotifications) : AnthropicModelCaller(engineNotifications)
{
    private readonly IEnumerable<IEngineNotification> _engineNotifications = engineNotifications;

    public FoundryAnthropicModelCaller()
        : this([]) { }

    public override (AnthropicClient client, string modelName) GetAnthropicClient(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields
    )
    {
        foreach (var notification in _engineNotifications)
        {
            var overrideClient = notification.FoundryAnthropicOverride(model, modelConfig, authenticationModeConfig, connectionFields);
            if (overrideClient is not null)
                return overrideClient.Value;
        }

        if (!model.ParameterValues.TryGetValue("deployment_name", out var deploymentName) || string.IsNullOrWhiteSpace(deploymentName))
            throw new SharpOMaticException("Model does not specify a deployment name");

        if (!connectionFields.TryGetValue("resource_name", out var resourceName) || string.IsNullOrWhiteSpace(resourceName))
            throw new SharpOMaticException("Connector resource name not specified.");

        resourceName = ValidateResourceName(resourceName);

        return authenticationModeConfig.Id switch
        {
            "api_key" => (new AnthropicFoundryClient(new AnthropicFoundryApiKeyCredentials(ResolveApiKey(connectionFields), resourceName)), deploymentName),
            "default_azure_credential" => (
                new AnthropicFoundryClient(new AnthropicFoundryIdentityTokenCredentials(new DefaultAzureCredential(), resourceName, ["https://ai.azure.com/.default"])),
                deploymentName
            ),
            _ => throw new SharpOMaticException($"Unsupported authentication method of '{authenticationModeConfig.Id}'"),
        };
    }

    private static string ResolveApiKey(Dictionary<string, string?> connectionFields)
    {
        if (!connectionFields.TryGetValue("api_key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new SharpOMaticException("Connector api key not specified.");

        return apiKey;
    }

    private static string ValidateResourceName(string resourceName)
    {
        resourceName = resourceName.Trim();
        if (resourceName.Length == 0)
            throw new SharpOMaticException("Connector resource name not specified.");

        if (resourceName.Any(char.IsWhiteSpace) || Uri.TryCreate(resourceName, UriKind.Absolute, out _))
            throw new SharpOMaticException("Connector resource name must be a Foundry resource name, not a URL.");

        return resourceName;
    }
}
