using System.Text.Json;
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

        var chatOptions = new ChatOptions();

        if (modelConfig.Capabilities.Any(c => c.Name == "SupportsStructuredOutput") &&
            Node.ParameterValues.TryGetValue("structured_output", out var outputFormat))

        {
            if (outputFormat == "Schema" &&
                Node.ParameterValues.TryGetValue("structured_output_schema", out var outputSchema) &&
                !string.IsNullOrWhiteSpace(outputSchema))
            {
                //JsonElement example = AIJsonUtilities.CreateJsonSchema(typeof(Response));
                //var responseString = JsonSerializer.Serialize(example);


                JsonElement element = JsonSerializer.Deserialize<JsonElement>(outputSchema);
                chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(element);
            }
        }

        // JsonElement schema = AIJsonUtilities.CreateJsonSchema(typeof(PersonInfo));

        if (modelConfig.Capabilities.Any(c => c.Name == "SupportsMaxOutputTokens") &&
            model.ParameterValues.TryGetValue("max_output_tokens", out var paramMaxOutputTokens) &&
            int.TryParse(paramMaxOutputTokens, out var maxOutputTokens))
        {
            chatOptions.MaxOutputTokens = maxOutputTokens;
        }

        OpenAIClient client = new OpenAIClient(apiKey);
        var chatCompletionClient = client.GetChatClient(modelName);

        var instructions = ContextHelpers.SubstituteValues(Node.Instructions, ThreadContext.NodeContext);
        var prompt = ContextHelpers.SubstituteValues(Node.Prompt, ThreadContext.NodeContext);

        AIAgent agent = chatCompletionClient.CreateAIAgent(instructions: instructions);
        var response = await agent.RunAsync(prompt, options: new ChatClientAgentRunOptions(chatOptions));
        
        var tempContext = new ContextObject();
        var textPath = !string.IsNullOrWhiteSpace(Node.TextOutputPath) ? Node.TextOutputPath : "output.text";
        tempContext.Set(textPath, response.Text);
        ThreadContext.RunContext.MergeContexts(ThreadContext.NodeContext, tempContext);

        return ("Model call executed", new List<NextNodeData> { new(ThreadContext, RunContext.ResolveSingleOutput(Node)) });
    }

    public class Response
    {
        public required string Answer { get; set; }
        public required string Reason { get; set; }
    }
}
