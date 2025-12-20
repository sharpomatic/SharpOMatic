#pragma warning disable OPENAI001

using OpenAI.Responses;
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

        var responseOptions = new ResponseCreationOptions();
        var chatOptions = new ChatOptions() { RawRepresentationFactory = (_) => responseOptions };

        if (modelConfig.Capabilities.Any(c => c.Name == "SupportsMaxOutputTokens") &&
            model.ParameterValues.TryGetValue("max_output_tokens", out var paramMaxOutputTokens) &&
            int.TryParse(paramMaxOutputTokens, out var maxOutputTokens))
        {
            chatOptions.MaxOutputTokens = maxOutputTokens;
        }

        if (modelConfig.Capabilities.Any(c => c.Name == "SupportsTemperature") &&
            model.ParameterValues.TryGetValue("temperature", out var paramTemperature) &&
            float.TryParse(paramTemperature, out var temperature))
        {
            chatOptions.Temperature = temperature;
        }

        if (modelConfig.Capabilities.Any(c => c.Name == "SupportsTopP") &&
            model.ParameterValues.TryGetValue("top_p", out var paramTopP) &&
            float.TryParse(paramTopP, out var topP))
        {
            chatOptions.TopP = topP;
        }

        if (modelConfig.Capabilities.Any(c => c.Name == "SupportsReasoningEffort") &&
            Node.ParameterValues.TryGetValue("reasoning_effort", out var outputEffort) &&
            !string.IsNullOrWhiteSpace(outputEffort))
        {
            responseOptions.ReasoningOptions = new ResponseReasoningOptions()
            {
                ReasoningEffortLevel = new ResponseReasoningEffortLevel(outputEffort.ToLower())
            };
        }

        if (modelConfig.Capabilities.Any(c => c.Name == "SupportsStructuredOutput") &&
            Node.ParameterValues.TryGetValue("structured_output", out var outputFormat) &&
            !string.IsNullOrWhiteSpace(outputFormat))
        {
            if (outputFormat == "Text")
                chatOptions.ResponseFormat = ChatResponseFormat.Text;
            else if (outputFormat == "Json")
                chatOptions.ResponseFormat = ChatResponseFormat.Json;
            else if (outputFormat == "Schema")
            {
                if (Node.ParameterValues.TryGetValue("structured_output_schema", out var outputSchema) &&
                    !string.IsNullOrWhiteSpace(outputSchema))
                {
                    Node.ParameterValues.TryGetValue("structured_output_schema_name", out var schemaName);
                    Node.ParameterValues.TryGetValue("structured_output_schema_description", out var schemaDescription);

                    if (string.IsNullOrWhiteSpace(schemaName))
                        schemaName = null;
                    else
                        schemaName = schemaName.Trim();

                    if (string.IsNullOrWhiteSpace(schemaDescription))
                        schemaDescription = null;
                    else
                        schemaDescription = schemaDescription.Trim();

                    var element = JsonSerializer.Deserialize<JsonElement>(outputSchema);
                    chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(element, schemaName: schemaName, schemaDescription: schemaDescription);
                }
            }
            else if (outputFormat == "Configured Type")
            {
                if (Node.ParameterValues.TryGetValue("structured_output_configured_type", out var configuredType) &&
                    !string.IsNullOrWhiteSpace(configuredType))
                {
                    Node.ParameterValues.TryGetValue("structured_output_schema_name", out var schemaName);
                    Node.ParameterValues.TryGetValue("structured_output_schema_description", out var schemaDescription);

                    var outputSchema = RunContext.TypeSchemaService.GetSchema(configuredType);
                    var element = JsonSerializer.Deserialize<JsonElement>(outputSchema);
                    chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(element, schemaName: schemaName, schemaDescription: schemaDescription);
                }
            }
            else
                throw new SharpOMaticException($"Unrecognized structured output setting of '{outputFormat}'");
        }

        OpenAIClient client = new OpenAIClient(apiKey);       
        var agentClient = client.GetOpenAIResponseClient(modelName);

        var instructions = ContextHelpers.SubstituteValues(Node.Instructions, ThreadContext.NodeContext);
        var prompt = ContextHelpers.SubstituteValues(Node.Prompt, ThreadContext.NodeContext);

        AIAgent agent = agentClient.CreateAIAgent(instructions: instructions);
        var response = await agent.RunAsync(prompt, options: new ChatClientAgentRunOptions(chatOptions));
        
        var tempContext = new ContextObject();
        var textPath = !string.IsNullOrWhiteSpace(Node.TextOutputPath) ? Node.TextOutputPath : "output.text";
        tempContext.Set(textPath, response.Text);
        ThreadContext.RunContext.MergeContexts(ThreadContext.NodeContext, tempContext);

        return ("Model call executed", new List<NextNodeData> { new(ThreadContext, RunContext.ResolveSingleOutput(Node)) });
    }
}
