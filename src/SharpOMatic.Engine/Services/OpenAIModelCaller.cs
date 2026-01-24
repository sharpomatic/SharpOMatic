#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Services;

public class OpenAIModelCaller : BaseModelCaller
{
    public override async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> Call(
        Model model, 
        ModelConfig modelConfig, 
        Connector connector, 
        ConnectorConfig connectorConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        // Get authentication method specific fields, allows for optional customization by user override mechanism
        (var authenticationModeConfig, var connectionFields) = GetAuthenticationFields(connector, connectorConfig, processContext);

        // Currently we only support models that can provide text input
        if (!HasCapability(model, modelConfig, "SupportsTextIn"))
            throw new SharpOMaticException("Model does not support text input.");

        // Setup the basic capabilities, then the more specialized options
        (var chatOptions, var responseCreationOptions) = SetupBasicCapabilities(model, modelConfig, processContext, threadContext, node);
        var jsonOutput = SetupStrucuturedOutput(chatOptions, model, modelConfig, processContext, node);
        var agentServiceProvider = SetupToolCalling(chatOptions, responseCreationOptions, model, modelConfig, processContext, threadContext, node);

        // Generate the chat messages for input to the model
        List<ChatMessage> chat = [];
        AddChatInputPathMessages(chat, threadContext, node);
        await AddImageMessages(chat, model, modelConfig, processContext, threadContext, node);

        // Resolve the instructions and prompts as templates
        var instructions = await ResolveInstructionsAndPrompt(chat, processContext, threadContext, node);
        var agentClient = GetOpenAIResponseClient(model, modelConfig, authenticationModeConfig, connectionFields);

        // Use the Microsoft Agent Framework by creating an agent from the responses AI client, then run the agent call
        AIAgent agent = agentClient.CreateAIAgent(instructions: instructions, services: agentServiceProvider);
        var response = await agent.RunAsync(chat, options: new ChatClientAgentRunOptions(chatOptions));
        var tempContext = ResponseToContextObject(jsonOutput, response, node);

        return (chat, response.Messages, tempContext);
    }

    public virtual OpenAIResponseClient GetOpenAIResponseClient(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields)
    {
        var options = new OpenAIClientOptions();
        if (!connectionFields.TryGetValue("api_key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new SharpOMaticException("Connector api key not specified.");

        string? modelName;
        if (modelConfig.IsCustom)
        {
            if (!model.ParameterValues.TryGetValue("model_name", out modelName) || string.IsNullOrWhiteSpace(modelName))
                throw new SharpOMaticException("Model does not specify the custom model name");
        }
        else
            modelName = modelConfig.DisplayName;

        if (connectionFields.TryGetValue("organization_id", out var organizationId) && !string.IsNullOrWhiteSpace(organizationId))
            options.OrganizationId = organizationId;

        if (connectionFields.TryGetValue("project_id", out var projectId) && !string.IsNullOrWhiteSpace(projectId))
            options.ProjectId = projectId;

        var client = new OpenAIClient(new ApiKeyCredential(apiKey ?? ""), options);
        return client.GetOpenAIResponseClient(modelName);
    }

    protected virtual (AuthenticationModeConfig, Dictionary<string, string?>) GetAuthenticationFields(
        Connector connector,
        ConnectorConfig connectorConfig,
        ProcessContext processContext)
    {
        var authenticationModel = connectorConfig.AuthModes.FirstOrDefault(a => a.Id == connector.AuthenticationModeId);
        if (authenticationModel is null)
            throw new SharpOMaticException("Connector has no selected authentication method");

        // Copy only the relevant connector fields into a new dictionary
        Dictionary<string, string?> connectionFields = [];
        foreach (var field in authenticationModel.Fields)
            if (connector.FieldValues.TryGetValue(field.Name, out var fieldValue))
                connectionFields.Add(field.Name, fieldValue);

        // Allow the user notifications to customize the field values
        var notifications = processContext.ServiceScope.ServiceProvider.GetServices<IEngineNotification>();
        foreach (var notification in notifications)
            notification.ConnectionOverride(processContext.Run.RunId, processContext.Run.WorkflowId, connector.ConfigId, authenticationModel, connectionFields);

        return (authenticationModel, connectionFields);
    }

    protected virtual (ChatOptions, ResponseCreationOptions) SetupBasicCapabilities(
        Model model,
        ModelConfig modelConfig,        
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        // Mostly we have set the ChatOptions but sometimes we have to drop down to the Responses AI specific ResponseCreationOptions
        var responseOptions = new ResponseCreationOptions();
        var chatOptions = new ChatOptions() { AdditionalProperties = [], RawRepresentationFactory = (_) => responseOptions };

        if (GetCapabilityInt(model, modelConfig, node, "SupportsMaxOutputTokens", "max_output_tokens", out int maxOutputTokens))
            chatOptions.MaxOutputTokens = maxOutputTokens;

        if (GetCapabilityString(model, modelConfig, node, "SupportsReasoningEffort", "reasoning_effort", out string reasoningEffort))
            responseOptions.ReasoningOptions = new ResponseReasoningOptions()
            {
                ReasoningEffortLevel = new ResponseReasoningEffortLevel(reasoningEffort.ToLower())
            };

        if (GetCapabilityFloat(model, modelConfig, node, "SupportsSampling", "temperature", out float temperature))
            chatOptions.Temperature = temperature;

        if (GetCapabilityFloat(model, modelConfig, node, "SupportsSampling", "top_p", out float topP))
            chatOptions.TopP = topP;

        return (chatOptions, responseOptions);
    }

    protected virtual bool SetupStrucuturedOutput(
        ChatOptions chatOptions,
        Model model,
        ModelConfig modelConfig,        
        ProcessContext processContext,
        ModelCallNodeEntity node)
    {
        bool jsonOutput = false;
        if (GetCapabilityString(model, modelConfig, node, "SupportsStructuredOutput", "structured_output", out string structuredOutput))
        {
            switch (structuredOutput)
            {
                case "Text":
                    // No structured output, just plain text output
                    chatOptions.ResponseFormat = ChatResponseFormat.Text;
                    break;

                case "Json":
                    // Json formatted output, but no schema defined
                    chatOptions.ResponseFormat = ChatResponseFormat.Json;
                    jsonOutput = true;
                    break;

                case "Schema":
                    // Json formatted output with manually defined schema provided by the user
                    if (node.ParameterValues.TryGetValue("structured_output_schema", out var outputSchema) &&
                        !string.IsNullOrWhiteSpace(outputSchema))
                    {
                        node.ParameterValues.TryGetValue("structured_output_schema_name", out var schemaName);
                        node.ParameterValues.TryGetValue("structured_output_schema_description", out var schemaDescription);

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
                    // Json formatted output with C# type as the definition to match
                    if (node.ParameterValues.TryGetValue("structured_output_configured_type", out var configuredType) &&
                        !string.IsNullOrWhiteSpace(configuredType))
                    {
                        node.ParameterValues.TryGetValue("structured_output_schema_name", out var schemaName);
                        node.ParameterValues.TryGetValue("structured_output_schema_description", out var schemaDescription);

                        var configuredSchema = processContext.SchemaTypeRegistry.GetSchema(configuredType);
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

        return jsonOutput;
    }

    protected virtual IServiceProvider SetupToolCalling (
        ChatOptions chatOptions,
        ResponseCreationOptions responseOptions,
        Model model,
        ModelConfig modelConfig,        
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        var agentServiceProvider = processContext.ServiceScope.ServiceProvider;
        if (HasCapability(model, modelConfig, "SupportsToolCalling"))
        {
            if (GetCapabilityBool(model, modelConfig, node, "SupportsToolCalling", "parallel_tool_calls", out bool parallelToolCalls))
                responseOptions.ParallelToolCallsEnabled = parallelToolCalls;

            if (GetCapabilityCallString(model, modelConfig, node, "SupportsToolCalling", "selected_tools", out string selectedTools))
            {
                agentServiceProvider = new OverlayServiceProvider(agentServiceProvider, threadContext.NodeContext);

                var toolNames = selectedTools.Split(',');
                List<AITool> tools = [];
                foreach (var toolName in toolNames)
                {
                    var toolDelegate = processContext.ToolMethodRegistry.GetToolFromDisplayName(toolName.Trim());
                    if (toolDelegate is null)
                        throw new SharpOMaticException($"Tool '{toolName.Trim()}' not found, check it is specified in the AddToolMethods setup.");

                    tools.Add(AIFunctionFactory.Create(toolDelegate));
                }

                if (tools.Count > 0)
                    chatOptions.Tools = tools;
            }

            if (GetCapabilityCallString(model, modelConfig, node, "SupportsToolCalling", "tool_choice", out string toolChoice))
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

        return agentServiceProvider;
    }

    protected virtual void AddChatInputPathMessages(
        List<ChatMessage> chat,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        if (!string.IsNullOrWhiteSpace(node.ChatInputPath))
        {
            if (threadContext.NodeContext.TryGet<ChatMessage>(node.ChatInputPath, out var chatMessage) && (chatMessage is not null))
                chat.Add(chatMessage);
            else if (threadContext.NodeContext.TryGet<ContextList>(node.ChatInputPath, out var chatList) && (chatList is not null))
            {
                foreach (var listEntry in chatList)
                    if ((listEntry is not null) && (listEntry is ChatMessage))
                        chat.Add((ChatMessage)listEntry);
            }
        }

    }

    protected virtual async Task AddImageMessages(
        List<ChatMessage> chat,
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        if (HasCapability(model, modelConfig, "SupportsImageIn"))
        {
            if (!string.IsNullOrWhiteSpace(node.ImageInputPath))
            {
                if (!threadContext.NodeContext.TryGet<object?>(node.ImageInputPath, out var imageValue) || imageValue is null)
                    throw new SharpOMaticException($"Image input path '{node.ImageInputPath}' could not be resolved.");

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
                            throw new SharpOMaticException($"Image input path '{node.ImageInputPath}' contains a non-asset entry at index {i}.");
                    }
                }
                else
                    throw new SharpOMaticException($"Image input path '{node.ImageInputPath}' must be an asset or asset list.");

                foreach (var entry in assetRefs)
                {
                    var asset = await processContext.RepositoryService.GetAsset(entry.AssetId);
                    if (!asset.MediaType.StartsWith("image/"))
                        throw new SharpOMaticException($"Asset '{entry.Name}' is not an image.");

                    await using var stream = await processContext.AssetStore.OpenReadAsync(asset.StorageKey);
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer);

                    var content = new DataContent(buffer.ToArray(), asset.MediaType) { Name = asset.Name };
                    chat.Add(new ChatMessage(ChatRole.User, [content]));
                }
            }
        }
    }

    protected virtual async Task<string?> ResolveInstructionsAndPrompt(
        List<ChatMessage> chat,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        string? instructions = null;
        if (!string.IsNullOrWhiteSpace(node.Instructions))
        {
            instructions = await ContextHelpers.SubstituteValuesAsync(
                node.Instructions,
                threadContext.NodeContext,
                processContext.RepositoryService,
                processContext.AssetStore,
                processContext.Run.RunId);
        }

        if (!string.IsNullOrWhiteSpace(node.Prompt))
        {
            var prompt = await ContextHelpers.SubstituteValuesAsync(
                node.Prompt,
                threadContext.NodeContext,
                processContext.RepositoryService,
                processContext.AssetStore,
                processContext.Run.RunId);

            chat.Add(new ChatMessage(ChatRole.User, [new TextContent(prompt)]));
        }

        return instructions;
    }

    protected virtual ContextObject ResponseToContextObject(
        bool jsonOutput,
        AgentRunResponse response,
        ModelCallNodeEntity node)
    {
        var tempContext = new ContextObject();
        var textPath = !string.IsNullOrWhiteSpace(node.TextOutputPath) ? node.TextOutputPath : "output.text";

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

        return tempContext;
    }
}
