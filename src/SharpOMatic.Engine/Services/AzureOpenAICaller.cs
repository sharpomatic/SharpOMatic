#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Services;

public class AzureOpenAIModelCaller : OpenAIModelCaller
{
    public override OpenAIResponseClient GetOpenAIResponseClient(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields
    )
    {
        if (!connectionFields.TryGetValue("endpoint", out var endpoint))
            throw new SharpOMaticException("Connector endpoint not specified.");

        if (!model.ParameterValues.TryGetValue("deployment_name", out var deploymentName))
            throw new SharpOMaticException("Model does not specify a deployment name");

        AzureOpenAIClient? azureClient = null;
        switch (authenticationModeConfig.Id)
        {
            case "api_key":
                if (!connectionFields.TryGetValue("api_key", out var apiKey))
                    throw new SharpOMaticException("Connector api key not specified.");

                azureClient = new(new Uri(endpoint ?? ""), new AzureKeyCredential(apiKey ?? ""));
                break;
            case "default_azure_credential":
                azureClient = new(new Uri(endpoint ?? ""), new DefaultAzureCredential());
                break;
            default:
                throw new SharpOMaticException(
                    $"Unsupported authentication method of '{authenticationModeConfig.Id}'"
                );
        }

        return azureClient.GetOpenAIResponseClient(deploymentName);
    }
}
