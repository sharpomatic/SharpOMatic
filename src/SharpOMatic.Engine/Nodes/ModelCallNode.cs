namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.ModelCall)]
public class ModelCallNode(ThreadContext threadContext, ModelCallNodeEntity node) : RunNode<ModelCallNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        // Validate and load the model and connector instances
        (var model, var modelConfig, var connector, var connectorConfig) = await LoadModelAndConnector();

        // Get the implementation for the specific connector config
        var caller = ProcessContext.ServiceScope.ServiceProvider.GetKeyedService<IModelCaller>(connectorConfig.ConfigId);
        if (caller is null)
            throw new SharpOMaticException($"No implementation found for connector '{connectorConfig.ConfigId}'");

        // Use specific implementation to perform call for us
        (var chat, var responses, var tempContext) = await caller.Call(model, modelConfig, connector, connectorConfig, ProcessContext, ThreadContext, Node);

        // Merge result context into the nodes context
        ProcessContext.MergeContexts(ThreadContext.NodeContext, tempContext);

        // If there is an output path for placing new chat history
        if (!string.IsNullOrWhiteSpace(Node.ChatOutputPath))
        {
            ContextList chatList = [];
            chatList.AddRange(chat);

            foreach (var message in responses)
                chatList.Add(message);

            ThreadContext.NodeContext.TrySet(Node.ChatOutputPath, chatList);
        }

        return ($"{model.Name ?? "(empty)"}", ResolveOptionalSingleOutput(ThreadContext));
    }

    private async Task<(Model, ModelConfig, Connector, ConnectorConfig)> LoadModelAndConnector()
    {
        if (Node.ModelId is null)
            throw new SharpOMaticException("No model selected");

        var model = await ProcessContext.RepositoryService.GetModel(Node.ModelId.Value);
        if (model is null)
            throw new SharpOMaticException("Cannot find model");

        if (model.ConnectorId is null)
            throw new SharpOMaticException("Model has no connector defined");

        var modelConfig = await ProcessContext.RepositoryService.GetModelConfig(model.ConfigId);
        if (modelConfig is null)
            throw new SharpOMaticException("Cannot find the model configuration");

        var connector = await ProcessContext.RepositoryService.GetConnector(model.ConnectorId.Value, false);
        if (connector is null)
            throw new SharpOMaticException("Cannot find the model connector");

        var connectorConfig = await ProcessContext.RepositoryService.GetConnectorConfig(connector.ConfigId);
        if (connectorConfig is null)
            throw new SharpOMaticException("Cannot find the connector configuration");

        return (model, modelConfig, connector, connectorConfig);
    }
}
