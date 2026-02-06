#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Services;

public abstract class BaseModelCaller : IModelCaller
{
    public abstract Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> Call(
        Model model,
        ModelConfig modelConfig,
        Connector connector,
        ConnectorConfig connectorConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node
    );

    protected virtual async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> CallAgent(
        AIAgent agent,
        List<ChatMessage> chat,
        ChatOptions? chatOptions,
        bool jsonOutput,
        ModelCallNodeEntity node
    )
    {
        var response = await agent.RunAsync(chat, options: new ChatClientAgentRunOptions(chatOptions));
        var tempContext = ResponseToContextObject(jsonOutput, response, node);
        return (chat, response.Messages, tempContext);
    }

    protected virtual async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> CallStreamingAgent(
        AIAgent agent,
        List<ChatMessage> chat,
        ChatOptions? chatOptions,
        bool jsonOutput,
        ModelCallNodeEntity node
    )
    {
        // Accumulate the streamed responses, but ignore messages that are empty
        List<ChatMessage> responses = [];
        await foreach (var message in agent.RunStreamingAsync(chat, options: new ChatClientAgentRunOptions(chatOptions)))
            if ((message.Contents is not null) && (message.Contents.Count > 0))
                responses.Add(new ChatMessage(message.Role ?? ChatRole.Assistant, message.Contents));

        var tempContext = ResponseToContextObject(jsonOutput, new AgentRunResponse(responses), node);
        return (chat, responses, tempContext);
    }

    protected virtual ChatOptions SetupBasicCapabilities(
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        ChatOptions? chatOptions = null
    )
    {
        chatOptions = chatOptions ?? new ChatOptions() { AdditionalProperties = [] };

        if (GetCapabilityInt(model, modelConfig, node, "SupportsMaxOutputTokens", "max_output_tokens", out int maxOutputTokens))
            chatOptions.MaxOutputTokens = maxOutputTokens;

        if (GetCapabilityFloat(model, modelConfig, node, "SupportsSampling", "temperature", out float temperature))
            chatOptions.Temperature = temperature;

        if (GetCapabilityFloat(model, modelConfig, node, "SupportsSampling", "top_p", out float topP))
            chatOptions.TopP = topP;

        return chatOptions;
    }

    protected virtual bool SetupStrucuturedOutput(ChatOptions chatOptions, Model model, ModelConfig modelConfig, ProcessContext processContext, ModelCallNodeEntity node)
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
                    if (node.ParameterValues.TryGetValue("structured_output_schema", out var outputSchema) && !string.IsNullOrWhiteSpace(outputSchema))
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
                    if (node.ParameterValues.TryGetValue("structured_output_configured_type", out var configuredType) && !string.IsNullOrWhiteSpace(configuredType))
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

    protected virtual IServiceProvider SetupToolCalling(
        ChatOptions chatOptions,
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node
    )
    {
        var agentServiceProvider = processContext.ServiceScope.ServiceProvider;
        if (HasCapability(model, modelConfig, "SupportsToolCalling"))
        {
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

                    tools.Add(AIFunctionFactory.Create(toolDelegate, toolName));
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

    protected virtual void AddChatInputPathMessages(List<ChatMessage> chat, ThreadContext threadContext, ModelCallNodeEntity node)
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

    protected virtual async Task AddImageMessages(List<ChatMessage> chat, Model model, ModelConfig modelConfig, ProcessContext processContext, ThreadContext threadContext, ModelCallNodeEntity node)
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

                    var content = new DataContent(buffer.ToArray(), asset.MediaType);
                    chat.Add(new ChatMessage(ChatRole.User, [content]));
                }
            }
        }
    }

    protected virtual async Task<string?> ResolveInstructionsAndPrompt(List<ChatMessage> chat, ProcessContext processContext, ThreadContext threadContext, ModelCallNodeEntity node)
    {
        string? instructions = null;
        if (!string.IsNullOrWhiteSpace(node.Instructions))
        {
            instructions = await ContextHelpers.SubstituteValuesAsync(
                node.Instructions,
                threadContext.NodeContext,
                processContext.RepositoryService,
                processContext.AssetStore,
                processContext.Run.RunId
            );
        }

        if (!string.IsNullOrWhiteSpace(node.Prompt))
        {
            var prompt = await ContextHelpers.SubstituteValuesAsync(node.Prompt, threadContext.NodeContext, processContext.RepositoryService, processContext.AssetStore, processContext.Run.RunId);

            chat.Add(new ChatMessage(ChatRole.User, [new TextContent(prompt)]));
        }

        return instructions;
    }

    protected static object? FastDeserializeString(string json)
    {
        var deserializer = new FastJsonDeserializer(json);
        return deserializer.Deserialize();
    }

    protected static bool HasCapability(Model model, ModelConfig modelConfig, string capability)
    {
        if ((model is null) || (modelConfig is null))
            return false;

        // If the config has the capability defined  (if custom then also selected in the model itself)
        return (modelConfig.Capabilities.Any(c => c.Name == capability) && (!modelConfig.IsCustom || (modelConfig.IsCustom && model.CustomCapabilities.Any(c => c == capability))));
    }

    protected static bool GetCapabilityString(Model model, ModelConfig modelConfig, ModelCallNodeEntity node, string capability, string field, out string paramValue)
    {
        if ((model is not null) && (modelConfig is not null) && HasCapability(model, modelConfig, capability))
        {
            var fieldDescription = modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                if (
                    (fieldDescription.CallDefined && node.ParameterValues.TryGetValue(field, out paramString) && !string.IsNullOrWhiteSpace(paramString))
                    || (!fieldDescription.CallDefined && model.ParameterValues.TryGetValue(field, out paramString) && !string.IsNullOrWhiteSpace(paramString))
                )
                {
                    paramValue = paramString;
                    return true;
                }
            }
        }

        paramValue = "";
        return false;
    }

    protected static bool GetCapabilityCallString(Model model, ModelConfig modelConfig, ModelCallNodeEntity node, string capability, string field, out string paramValue)
    {
        if (
            (model is not null)
            && (modelConfig is not null)
            && HasCapability(model, modelConfig, capability)
            && node.ParameterValues.TryGetValue(field, out var paramString)
            && !string.IsNullOrWhiteSpace(paramString)
        )
        {
            paramValue = paramString;
            return true;
        }

        paramValue = "";
        return false;
    }

    protected static bool GetCapabilityInt(Model model, ModelConfig modelConfig, ModelCallNodeEntity node, string capability, string field, out int paramValue)
    {
        if ((model is not null) && (modelConfig is not null) && HasCapability(model, modelConfig, capability))
        {
            var fieldDescription = modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                int paramInteger;
                if (
                    (fieldDescription.CallDefined && node.ParameterValues.TryGetValue(field, out paramString) && int.TryParse(paramString, out paramInteger))
                    || (!fieldDescription.CallDefined && model.ParameterValues.TryGetValue(field, out paramString) && int.TryParse(paramString, out paramInteger))
                )
                {
                    paramValue = paramInteger;
                    return true;
                }
            }
        }

        paramValue = 0;
        return false;
    }

    protected static bool GetCapabilityFloat(Model model, ModelConfig modelConfig, ModelCallNodeEntity node, string capability, string field, out float paramValue)
    {
        if ((model is not null) && (modelConfig is not null) && HasCapability(model, modelConfig, capability))
        {
            var fieldDescription = modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                float paramFloat;
                if (
                    (fieldDescription.CallDefined && node.ParameterValues.TryGetValue(field, out paramString) && float.TryParse(paramString, out paramFloat))
                    || (!fieldDescription.CallDefined && model.ParameterValues.TryGetValue(field, out paramString) && float.TryParse(paramString, out paramFloat))
                )
                {
                    paramValue = paramFloat;
                    return true;
                }
            }
        }

        paramValue = 0;
        return false;
    }

    protected static bool GetCapabilityBool(Model model, ModelConfig modelConfig, ModelCallNodeEntity node, string capability, string field, out bool paramValue)
    {
        if ((model is not null) && (modelConfig is not null) && HasCapability(model, modelConfig, capability))
        {
            var fieldDescription = modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                bool paramBool;
                if (
                    (fieldDescription.CallDefined && node.ParameterValues.TryGetValue(field, out paramString) && bool.TryParse(paramString, out paramBool))
                    || (!fieldDescription.CallDefined && model.ParameterValues.TryGetValue(field, out paramString) && bool.TryParse(paramString, out paramBool))
                )
                {
                    paramValue = paramBool;
                    return true;
                }
            }
        }

        paramValue = false;
        return false;
    }

    protected virtual ContextObject ResponseToContextObject(bool jsonOutput, AgentRunResponse response, ModelCallNodeEntity node)
    {
        var tempContext = new ContextObject();
        var textPath = !string.IsNullOrWhiteSpace(node.TextOutputPath) ? node.TextOutputPath : "output.text";

        StringBuilder sb = new();
        foreach (var message in response.Messages)
            if (!string.IsNullOrEmpty(message.Text))
                sb.Append(message.Text);

        if (jsonOutput)
        {
            try
            {
                var objects = FastDeserializeString(sb.ToString());
                tempContext.Set(textPath, objects);
            }
            catch
            {
                throw new SharpOMaticException($"Model response could not be parsed as json.");
            }
        }
        else
            tempContext.Set(textPath, sb.ToString());

        return tempContext;
    }
}
