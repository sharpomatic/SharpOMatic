#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.ModelCall)]
public class ModelCallNode(ThreadContext threadContext, ModelCallNodeEntity node) 
    : RunNode<ModelCallNodeEntity>(threadContext, node)
{
    private Model? _model;
    private ModelConfig? _modelConfig;

    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        var repository = ProcessContext.RepositoryService;
        var assetStore = ProcessContext.ServiceScope.ServiceProvider.GetRequiredService<IAssetStore>();

        if (Node.ModelId is null)
            throw new SharpOMaticException("No model selected");

        _model = await repository.GetModel(Node.ModelId.Value);
        if (_model is null)
            throw new SharpOMaticException("Cannot find model");

        if (_model.ConnectorId is null)
            throw new SharpOMaticException("Model has no connector defined");

        _modelConfig = await repository.GetModelConfig(_model.ConfigId);
        if (_modelConfig is null)
            throw new SharpOMaticException("Cannot find the model configuration");

        var connector = await repository.GetConnector(_model.ConnectorId.Value, false);
        if (connector is null)
            throw new SharpOMaticException("Cannot find the model connector");

        var connectorConfig = await repository.GetConnectorConfig(connector.ConfigId);
        if (connectorConfig is null)
            throw new SharpOMaticException("Cannot find the connector configuration");

        var selectedAuthenticationModel = connectorConfig.AuthModes.FirstOrDefault(a => a.Id == connector.AuthenticationModeId);
        if (selectedAuthenticationModel is null)
            throw new SharpOMaticException("Connector has no selected authentication method");

        // Copy only the relevant connector fields into a new dictionary
        Dictionary<string, string?> connectionFields = [];
        foreach(var field in selectedAuthenticationModel.Fields)
            if (connector.FieldValues.TryGetValue(field.Name, out var fieldValue))
                connectionFields.Add(field.Name, fieldValue);

        // Allow the user notifications to customize the field values
        var notifications = ProcessContext.ServiceScope.ServiceProvider.GetServices<IEngineNotification>();
        foreach (var notification in notifications)
            notification.ConnectionOverride(ProcessContext.Run.RunId, ProcessContext.Run.WorkflowId, connector.ConfigId, selectedAuthenticationModel, connectionFields);

        if (!HasCapability("SupportsTextIn"))
            throw new SharpOMaticException("Model does not support text input.");

        var responseOptions = new ResponseCreationOptions();
        var chatOptions = new ChatOptions() { AdditionalProperties = [], RawRepresentationFactory = (_) => responseOptions };

        if (GetCapabilityInt("SupportsMaxOutputTokens", "max_output_tokens", out int maxOutputTokens))
            chatOptions.MaxOutputTokens = maxOutputTokens;

        if (GetCapabilityString("SupportsReasoningEffort", "reasoning_effort", out string reasoningEffort))
            responseOptions.ReasoningOptions = new ResponseReasoningOptions() { ReasoningEffortLevel = new ResponseReasoningEffortLevel(reasoningEffort.ToLower()) };

        if (GetCapabilityFloat("SupportsSampling", "temperature", out float temperature))
            chatOptions.Temperature = temperature;

        if (GetCapabilityFloat("SupportsSampling", "top_p", out float topP))
            chatOptions.TopP = topP;

        bool jsonOutput = false;
        if (GetCapabilityString("SupportsStructuredOutput", "structured_output", out string structuredOutput))
        {
            switch (structuredOutput)
            {
                case "Text":
                    chatOptions.ResponseFormat = ChatResponseFormat.Text;
                    break;

                case "Json":
                    chatOptions.ResponseFormat = ChatResponseFormat.Json;
                    jsonOutput = true;
                    break;

                case "Schema":
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
                        jsonOutput = true;
                    }
                    break;
                case "Configured Type":
                    if (Node.ParameterValues.TryGetValue("structured_output_configured_type", out var configuredType) &&
                        !string.IsNullOrWhiteSpace(configuredType))
                    {
                        Node.ParameterValues.TryGetValue("structured_output_schema_name", out var schemaName);
                        Node.ParameterValues.TryGetValue("structured_output_schema_description", out var schemaDescription);

                        var configuredSchema = ProcessContext.SchemaTypeRegistry.GetSchema(configuredType);
                        if (string.IsNullOrWhiteSpace(configuredSchema))
                            throw new SharpOMaticException($"Configured type '{configuredType}' not found, check it is specified in the AddSchemaTypes setup.");

                        var element = JsonSerializer.Deserialize<JsonElement>(configuredSchema);
                        chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(element, schemaName: schemaName, schemaDescription: schemaDescription);
                        jsonOutput = true;
                    }
                    break;

                default:
                    throw new SharpOMaticException($"Unrecognized structured output setting of '{structuredOutput}'");
            }
        }

        var agentServiceProvider = ProcessContext.ServiceScope.ServiceProvider;
        if (HasCapability("SupportsToolCalling"))
        {
            if (GetCapabilityBool("SupportsToolCalling", "parallel_tool_calls", out bool parallelToolCalls))
                responseOptions.ParallelToolCallsEnabled = parallelToolCalls;

            if (GetCapabilityCallString("SupportsToolCalling", "selected_tools", out string selectedTools))
            {
                agentServiceProvider = new OverlayServiceProvider(agentServiceProvider, ThreadContext.NodeContext);

                var toolNames = selectedTools.Split(',');
                List<AITool> tools = [];
                foreach (var toolName in toolNames)
                {
                    var toolDelegate = ProcessContext.ToolMethodRegistry.GetToolFromDisplayName(toolName.Trim());
                    if (toolDelegate is null)
                        throw new SharpOMaticException($"Tool '{toolName.Trim()}' not found, check it is specified in the AddToolMethods setup.");

                    tools.Add(AIFunctionFactory.Create(toolDelegate));
                }

                if (tools.Count > 0)
                    chatOptions.Tools = tools;
            }

            if (GetCapabilityCallString("SupportsToolCalling", "tool_choice", out string toolChoice))
            {
                switch (toolChoice)
                {
                    case "None":
                        chatOptions.ToolMode = ChatToolMode.None;
                        break;

                    case "Auto":
                        chatOptions.ToolMode = ChatToolMode.Auto;
                        break;

                    default:
                        throw new SharpOMaticException($"Unrecognized tool choice setting of '{toolChoice}'");
                }
            }
        }

        List<ChatMessage> chat = [];

        if (!string.IsNullOrWhiteSpace(Node.ChatInputPath))
        {
            if (ThreadContext.NodeContext.TryGet<ChatMessage>(Node.ChatInputPath, out var chatMessage) && (chatMessage is not null))
                chat.Add(chatMessage);
            else if (ThreadContext.NodeContext.TryGet<ContextList>(Node.ChatInputPath, out var chatList) && (chatList is not null))
            {
                foreach(var listEntry in chatList)
                    if ((listEntry is not null) && (listEntry is ChatMessage))
                        chat.Add((ChatMessage)listEntry);
            }
        }

        string? instructions = null;
        if (!string.IsNullOrWhiteSpace(Node.Instructions))
            instructions = await ContextHelpers.SubstituteValuesAsync(
                Node.Instructions,
                ThreadContext.NodeContext,
                repository,
                assetStore,
                ProcessContext.Run.RunId);

        if (!string.IsNullOrWhiteSpace(Node.Prompt))
        {
            var prompt = await ContextHelpers.SubstituteValuesAsync(
                Node.Prompt,
                ThreadContext.NodeContext,
                repository,
                assetStore,
                ProcessContext.Run.RunId);
            chat.Add(new ChatMessage(ChatRole.User, [new TextContent(prompt)]));
        }

        if (HasCapability("SupportsImageIn"))
        {
            if (!string.IsNullOrWhiteSpace(Node.ImageInputPath))
            {
                if (!ThreadContext.NodeContext.TryGet<object?>(Node.ImageInputPath, out var imageValue) || imageValue is null)
                    throw new SharpOMaticException($"Image input path '{Node.ImageInputPath}' could not be resolved.");

                List<AssetRef> assetRefs = [];
                if (imageValue is AssetRef assetRef)
                    assetRefs.Add(assetRef);
                else if (imageValue is ContextList assetList)
                {
                    for (var i = 0; i < assetList.Count; i += 1)
                    {
                        var item = assetList[i];
                        if (item is AssetRef listAssetRef)
                            assetRefs.Add(listAssetRef);
                        else
                            throw new SharpOMaticException($"Image input path '{Node.ImageInputPath}' contains a non-asset entry at index {i}.");
                    }
                }
                else
                    throw new SharpOMaticException($"Image input path '{Node.ImageInputPath}' must be an asset or asset list.");

                foreach (var entry in assetRefs)
                {
                    var asset = await repository.GetAsset(entry.AssetId);
                    if (!asset.MediaType.StartsWith("image/"))
                        throw new SharpOMaticException($"Asset '{entry.Name}' is not an image.");

                    await using var stream = await assetStore.OpenReadAsync(asset.StorageKey);
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer);

                    var content = new DataContent(buffer.ToArray(), asset.MediaType) { Name = asset.Name };
                    chat.Add(new ChatMessage(ChatRole.User, [content]));
                }
            }
        }

        OpenAIResponseClient? agentClient = null;
        var modelName = "";

        switch (connectorConfig.ConfigId)
        {
            case "openai":
                {
                    var options = new OpenAIClientOptions();

                    if (!connectionFields.TryGetValue("api_key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
                        throw new SharpOMaticException("Connector api key not specified.");

                    if (_modelConfig.IsCustom)
                    {
                        if (!_model.ParameterValues.TryGetValue("model_name", out modelName) || string.IsNullOrWhiteSpace(modelName))
                            throw new SharpOMaticException("Model does not specify the custom model name");
                    }
                    else
                        modelName = _modelConfig.DisplayName;

                    if (connectionFields.TryGetValue("organization_id", out var organizationId) && !string.IsNullOrWhiteSpace(organizationId))
                        options.OrganizationId = organizationId;

                    if (connectionFields.TryGetValue("project_id", out var projectId) && !string.IsNullOrWhiteSpace(projectId))
                        options.ProjectId = projectId;

                    var client = new OpenAIClient(new ApiKeyCredential(apiKey ?? ""), options);
                    agentClient = client.GetOpenAIResponseClient(modelName);
                }
                break;
            case "azure_openai":
                {
                    if (!connectionFields.TryGetValue("endpoint", out var endpoint))
                        throw new SharpOMaticException("Connector endpoint not specified.");

                    if (!_model.ParameterValues.TryGetValue("deployment_name", out var deploymentName))
                        throw new SharpOMaticException("Model does not specify a deployment name");

                    AzureOpenAIClient? azureClient = null;
                    switch (selectedAuthenticationModel.Id)
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
                            throw new SharpOMaticException($"Unsupported authentication method of '{selectedAuthenticationModel.Id}'");
                    }

                    agentClient = azureClient.GetOpenAIResponseClient(deploymentName);
                }
                break;
            default:
                throw new SharpOMaticException($"Unsupported connector config of '{connectorConfig.ConfigId}'");
        }

        AIAgent agent = agentClient.CreateAIAgent(instructions: instructions, services: agentServiceProvider);
        var response = await agent.RunAsync(chat, options: new ChatClientAgentRunOptions(chatOptions));

        var tempContext = new ContextObject();
        var textPath = !string.IsNullOrWhiteSpace(Node.TextOutputPath) ? Node.TextOutputPath : "output.text";

        if (jsonOutput)
        {
            try
            {
                var objects = FastDeserializeString(response.Text);
                tempContext.Set(textPath, objects);
            }
            catch
            {
                throw new SharpOMaticException($"Model response could not be parsed as json.");
            }
        }
        else
            tempContext.Set(textPath, response.Text);

        ProcessContext.MergeContexts(ThreadContext.NodeContext, tempContext);

        if (!string.IsNullOrWhiteSpace(Node.ChatOutputPath))
        {
            ContextList chatList = [];
            chatList.AddRange(chat);

            foreach (var message in response.Messages)
                chatList.Add(message);

            ThreadContext.NodeContext.TrySet(Node.ChatOutputPath, chatList);
        }

        return ($"Model {_model.Name ?? "(empty)"} called", ResolveOptionalSingleOutput(ThreadContext));
    }

    private static object? FastDeserializeString(string json)
    {
        var deserializer = new FastJsonDeserializer(json);
        return deserializer.Deserialize();
    }

    private bool HasCapability(string capability)
    {
        if ((_model is null) || ( _modelConfig is null))
            return false;

        // If the config has the capability defined  (if custom then also selected in the model itself)
        return (_modelConfig.Capabilities.Any(c => c.Name == capability) && (!_modelConfig.IsCustom ||
               (_modelConfig.IsCustom && _model.CustomCapabilities.Any(c => c == capability))));
    }

    private bool GetCapabilityString(string capability, string field, out string paramValue)
    {
        if ((_model is not null) &&
            (_modelConfig is not null) &&
            HasCapability(capability))
        {
            var fieldDescription = _modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                if ((fieldDescription.CallDefined && Node.ParameterValues.TryGetValue(field, out paramString) && !string.IsNullOrWhiteSpace(paramString)) ||
                    (!fieldDescription.CallDefined && _model.ParameterValues.TryGetValue(field, out paramString) && !string.IsNullOrWhiteSpace(paramString)))
                {
                    paramValue = paramString;
                    return true;
                }
            }
        }

        paramValue = "";
        return false;
    }

    private bool GetCapabilityCallString(string capability, string field, out string paramValue)
    {
        if ((_model is not null) &&
            (_modelConfig is not null) &&
            HasCapability(capability) &&
            Node.ParameterValues.TryGetValue(field, out var paramString) && 
            !string.IsNullOrWhiteSpace(paramString))
        {
            paramValue = paramString;
            return true;
        }

        paramValue = "";
        return false;
    }

    private bool GetCapabilityInt(string capability, string field, out int paramValue)
    {
        if ((_model is not null) &&
            (_modelConfig is not null) &&
            HasCapability(capability))
        {
            var fieldDescription = _modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                int paramInteger;
                if ((fieldDescription.CallDefined && Node.ParameterValues.TryGetValue(field, out paramString) && int.TryParse(paramString, out paramInteger)) ||
                    (!fieldDescription.CallDefined && _model.ParameterValues.TryGetValue(field, out paramString) && int.TryParse(paramString, out paramInteger)))
                {
                    paramValue = paramInteger;
                    return true;
                }
            }
        }

        paramValue = 0;
        return false;
    }

    private bool GetCapabilityFloat(string capability, string field, out float paramValue)
    {
        if ((_model is not null) &&
            (_modelConfig is not null) &&
            HasCapability(capability))
        {
            var fieldDescription = _modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                float paramFloat;
                if ((fieldDescription.CallDefined && Node.ParameterValues.TryGetValue(field, out paramString) && float.TryParse(paramString, out paramFloat)) ||
                    (!fieldDescription.CallDefined && _model.ParameterValues.TryGetValue(field, out paramString) && float.TryParse(paramString, out paramFloat)))
                {
                    paramValue = paramFloat;
                    return true;
                }
            }
        }

        paramValue = 0;
        return false;
    }

    private bool GetCapabilityBool(string capability, string field, out bool paramValue)
    {
        if ((_model is not null) &&
            (_modelConfig is not null) &&
            HasCapability(capability))
        {
            var fieldDescription = _modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                bool paramBool;
                if ((fieldDescription.CallDefined && Node.ParameterValues.TryGetValue(field, out paramString) && bool.TryParse(paramString, out paramBool)) ||
                    (!fieldDescription.CallDefined && _model.ParameterValues.TryGetValue(field, out paramString) && bool.TryParse(paramString, out paramBool)))
                {
                    paramValue = paramBool;
                    return true;
                }
            }
        }

        paramValue = false;
        return false;
    }
}
