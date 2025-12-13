namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.ModelCall)]
public class ModelCallNode(ThreadContext threadContext, ModelCallNodeEntity node) : RunNode<ModelCallNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        var repository = ThreadContext.RunContext.Repository;

        if (Node.ModelId is null)
            throw new SharpOMaticException("No model selected");

        var model = await repository.GetModel(Node.ModelId.Value);
        if (model is null)
            throw new SharpOMaticException("Cannot find model");

        if (model.ConnectorId is null)
            throw new SharpOMaticException("Model has no connector defined");

        var modelConfig = await repository.GetModelConfig(model.ConfigId);
        if (modelConfig is null)
            throw new SharpOMaticException("Cannot find the model configuration");

        var connector = await repository.GetConnector(model.ConnectorId.Value, false);
        if (connector is null)
            throw new SharpOMaticException("Cannot find the model connector");

        var connectorConfig = await repository.GetConnectorConfig(connector.ConfigId);
        if (connectorConfig is null)
            throw new SharpOMaticException("Cannot find the connector configuration");

        var selectedAuthenticationModel = connectorConfig.AuthModes.FirstOrDefault(a => a.Id == connector.AuthenticationModeId);
        if (selectedAuthenticationModel is null)
            throw new SharpOMaticException("Connector has no selected authentication method");

        if (selectedAuthenticationModel.Id != "apikey")
            throw new SharpOMaticException("Only connector apikey authentication is currently supported");

        if (!connector.FieldValues.TryGetValue("apikey", out var apiKey))
            throw new SharpOMaticException("Connector api key not specified.");

        if (!modelConfig.Capabilities.Any(c => c.Name == "SupportsText"))
            throw new SharpOMaticException("Model does not support text");

        var modelName = "";
        if (modelConfig.IsCustom)
        {
            if (!model.ParameterValues.TryGetValue("modelName", out modelName))
                throw new SharpOMaticException("Model does not specify the custom model name");
        }
        else
            modelName = modelConfig.DisplayName;

        OpenAIClient client = new OpenAIClient(apiKey);
        var chatCompletionClient = client.GetChatClient(modelName);

        AIAgent agent = chatCompletionClient.CreateAIAgent(instructions: Node.Instructions);
        var response = await agent.RunAsync(Node.Prompt);
        ThreadContext.NodeContext.Set("output.text", response.Text);

        return ("Model call executed", new List<NextNodeData> { new(ThreadContext, RunContext.ResolveSingleOutput(Node)) });
    }
}
