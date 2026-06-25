#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Services;

public abstract class BaseModelCaller : IModelCaller
{
    private const string GracefulStopToolResultSentinel = "__SharpOMaticModelCallGracefulStop__";

    public abstract Task<ModelCallResult> Call(
        Model model,
        ModelConfig modelConfig,
        Connector connector,
        ConnectorConfig connectorConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        IModelCallProgressSink progressSink
    );

    protected virtual async Task<ModelCallResult> CallAgent(AIAgent agent, List<ChatMessage> chat, ChatOptions? chatOptions, bool jsonOutput, ModelCallNodeEntity node)
    {
        AgentResponse response;
        try
        {
            response = await agent.RunAsync(chat, options: new ChatClientAgentRunOptions(chatOptions));
        }
        catch (Exception ex) when (TryGetGracefulStopException(ex, out _))
        {
            return new ModelCallResult()
            {
                Chat = chat,
                Responses = [],
                ResultValue = string.Empty,
            };
        }

        var stoppedGracefully = RemoveGracefulStopToolResults(response.Messages);
        var resultValue = ResponseToOutputValue(jsonOutput && !stoppedGracefully, response);
        return new ModelCallResult()
        {
            Chat = chat,
            Responses = response.Messages,
            ResultValue = resultValue,
            Usage = response.Usage,
        };
    }

    protected virtual async Task<ModelCallResult> CallStreamingAgent(
        AIAgent agent,
        List<ChatMessage> chat,
        ChatOptions? chatOptions,
        bool jsonOutput,
        ModelCallNodeEntity node,
        IModelCallProgressSink progressSink
    )
    {
        List<AgentResponseUpdate> updates = [];
        string syntheticMessageId = $"assistant-{Guid.NewGuid():N}";
        string? currentAssistantMessageId = null;
        bool stoppedGracefully = false;

        try
        {
            await foreach (var update in agent.RunStreamingAsync(chat, options: new ChatClientAgentRunOptions(chatOptions)))
            {
                updates.Add(update);
                var messageId = ResolveMessageId(update, syntheticMessageId);

                if ((update.Role is not null) && (update.Role != ChatRole.Assistant) && (currentAssistantMessageId is not null))
                {
                    await progressSink.OnTextEndAsync(currentAssistantMessageId);
                    currentAssistantMessageId = null;
                }

                if ((update.Role is null || update.Role == ChatRole.Assistant) && !string.Equals(currentAssistantMessageId, messageId, StringComparison.Ordinal))
                {
                    if (currentAssistantMessageId is not null)
                        await progressSink.OnTextEndAsync(currentAssistantMessageId);

                    currentAssistantMessageId = null;
                }

                bool handledTextContent = false;
                if ((update.Contents is not null) && (update.Contents.Count > 0))
                {
                    foreach (var content in update.Contents)
                    {
                        switch (content)
                        {
                            case TextContent textContent when (update.Role is null || update.Role == ChatRole.Assistant) && !string.IsNullOrWhiteSpace(textContent.Text):
                                if (currentAssistantMessageId is null)
                                {
                                    await progressSink.OnTextStartAsync(messageId);
                                    currentAssistantMessageId = messageId;
                                }

                                await progressSink.OnTextDeltaAsync(messageId, textContent.Text);
                                handledTextContent = true;
                                break;

                            case TextReasoningContent reasoningContent when update.Role is null || update.Role == ChatRole.Assistant:
                                await progressSink.OnReasoningAsync(BuildReasoningId(messageId), reasoningContent.Text ?? string.Empty);
                                break;

                            case FunctionCallContent functionCallContent:
                                await progressSink.OnToolCallAsync(
                                    BuildToolCallId(messageId, functionCallContent),
                                    functionCallContent.Name,
                                    SerializeToolArguments(functionCallContent),
                                    messageId,
                                    SerializeToolCall(functionCallContent)
                                );
                                break;

                            case FunctionResultContent functionResultContent when IsGracefulStopToolResult(functionResultContent):
                                stoppedGracefully = true;
                                break;

                            case FunctionResultContent functionResultContent:
                                var toolResultMessageId = ResolveToolResultMessageId(update, functionResultContent, syntheticMessageId);
                                await progressSink.OnToolCallResultAsync(
                                    toolResultMessageId,
                                    ResolveToolResultCallId(functionResultContent, toolResultMessageId),
                                    SerializeToolResult(functionResultContent)
                                );
                                break;
                        }
                    }
                }

                if ((update.Role is null || update.Role == ChatRole.Assistant) && !handledTextContent && !string.IsNullOrWhiteSpace(update.Text))
                {
                    if (currentAssistantMessageId is null)
                    {
                        await progressSink.OnTextStartAsync(messageId);
                        currentAssistantMessageId = messageId;
                    }

                    await progressSink.OnTextDeltaAsync(messageId, update.Text);
                }
            }
        }
        catch (Exception ex) when (TryGetGracefulStopException(ex, out _))
        {
            stoppedGracefully = true;
        }
        finally
        {
            if (currentAssistantMessageId is not null)
                await progressSink.OnTextEndAsync(currentAssistantMessageId);

            await progressSink.CompleteAsync();
        }

        var response = updates.ToAgentResponse();
        stoppedGracefully |= RemoveGracefulStopToolResults(response.Messages);
        var resultValue = ResponseToOutputValue(jsonOutput && !stoppedGracefully, response);
        return new ModelCallResult()
        {
            Chat = chat,
            Responses = response.Messages,
            ResultValue = resultValue,
            Usage = response.Usage,
        };
    }

    protected virtual IChatClient CreateFunctionInvokingChatClient(IChatClient chatClient, IServiceProvider? toolServiceProvider)
    {
        return new FunctionInvokingChatClient(chatClient, loggerFactory: null, functionInvocationServices: toolServiceProvider)
        {
            FunctionInvoker = async (context, cancellationToken) =>
            {
                try
                {
                    return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
                }
                catch (Exception ex) when (TryGetGracefulStopException(ex, out _))
                {
                    context.Terminate = true;
                    return GracefulStopToolResultSentinel;
                }
            },
        };
    }

    protected static bool TryGetGracefulStopException(Exception exception, [NotNullWhen(true)] out ModelCallGracefulStopException? gracefulStopException)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ModelCallGracefulStopException modelCallGracefulStopException)
            {
                gracefulStopException = modelCallGracefulStopException;
                return true;
            }

            if (current is AggregateException aggregateException)
            {
                foreach (var innerException in aggregateException.Flatten().InnerExceptions)
                {
                    if (TryGetGracefulStopException(innerException, out gracefulStopException))
                        return true;
                }
            }
        }

        gracefulStopException = null;
        return false;
    }

    private static bool RemoveGracefulStopToolResults(IList<ChatMessage> messages)
    {
        var removed = false;
        for (var messageIndex = messages.Count - 1; messageIndex >= 0; messageIndex -= 1)
        {
            var message = messages[messageIndex];
            for (var contentIndex = message.Contents.Count - 1; contentIndex >= 0; contentIndex -= 1)
            {
                if (message.Contents[contentIndex] is not FunctionResultContent functionResultContent || !IsGracefulStopToolResult(functionResultContent))
                    continue;

                message.Contents.RemoveAt(contentIndex);
                removed = true;
            }

            if (message.Contents.Count == 0)
                messages.RemoveAt(messageIndex);
        }

        return removed;
    }

    private static bool IsGracefulStopToolResult(FunctionResultContent functionResultContent)
    {
        return functionResultContent.Result switch
        {
            string result => string.Equals(result, GracefulStopToolResultSentinel, StringComparison.Ordinal),
            JsonElement { ValueKind: JsonValueKind.String } element => string.Equals(element.GetString(), GracefulStopToolResultSentinel, StringComparison.Ordinal),
            _ => false,
        };
    }

    protected virtual Task<ModelCallResult> CallConfiguredAgent(
        AIAgent agent,
        List<ChatMessage> chat,
        ChatOptions? chatOptions,
        bool jsonOutput,
        ModelCallNodeEntity node,
        IModelCallProgressSink progressSink
    )
    {
        return node.BatchOutput ? CallAgent(agent, chat, chatOptions, jsonOutput, node) : CallStreamingAgent(agent, chat, chatOptions, jsonOutput, node, progressSink);
    }

    protected virtual string ResolveMessageId(AgentResponseUpdate update, string syntheticMessageId)
    {
        if (!string.IsNullOrWhiteSpace(update.MessageId))
            return update.MessageId.Trim();

        if (!string.IsNullOrWhiteSpace(update.ResponseId))
            return update.ResponseId.Trim();

        return syntheticMessageId;
    }

    protected virtual string BuildReasoningId(string messageId)
    {
        return messageId;
    }

    protected virtual string BuildToolCallId(string messageId, FunctionCallContent functionCallContent)
    {
        if (!string.IsNullOrWhiteSpace(functionCallContent.CallId))
            return functionCallContent.CallId.Trim();

        return $"tool:{messageId}:{functionCallContent.Name}";
    }

    protected virtual string ResolveToolResultCallId(FunctionResultContent functionResultContent, string fallbackMessageId)
    {
        if (!string.IsNullOrWhiteSpace(functionResultContent.CallId))
            return functionResultContent.CallId.Trim();

        return $"tool-result:{fallbackMessageId}";
    }

    protected virtual string ResolveToolResultMessageId(AgentResponseUpdate update, FunctionResultContent functionResultContent, string syntheticMessageId)
    {
        if (!string.IsNullOrWhiteSpace(update.MessageId))
            return update.MessageId.Trim();

        if (!string.IsNullOrWhiteSpace(update.ResponseId))
            return update.ResponseId.Trim();

        return $"tool-result:{syntheticMessageId}";
    }

    protected virtual string? SerializeToolArguments(FunctionCallContent functionCallContent)
    {
        if (functionCallContent.Arguments is null)
            return null;

        try
        {
            return JsonSerializer.Serialize(functionCallContent.Arguments);
        }
        catch
        {
            return functionCallContent.Arguments.ToString();
        }
    }

    protected virtual string? SerializeToolCall(FunctionCallContent functionCallContent)
    {
        try
        {
            return JsonSerializer.Serialize(
                new
                {
                    functionCallContent.CallId,
                    functionCallContent.Name,
                    functionCallContent.Arguments,
                }
            );
        }
        catch
        {
            return null;
        }
    }

    protected virtual string SerializeToolResult(FunctionResultContent functionResultContent)
    {
        if (functionResultContent.Result is null)
            return string.Empty;

        if (functionResultContent.Result is string resultText)
            return resultText;

        try
        {
            return JsonSerializer.Serialize(functionResultContent.Result);
        }
        catch
        {
            return functionResultContent.Result.ToString() ?? string.Empty;
        }
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
                agentServiceProvider = new OverlayServiceProvider(agentServiceProvider, threadContext.NodeContext, new StreamEventHelper(processContext, threadContext.NodeContext));

                var toolNames = selectedTools.Split(',');
                List<AITool> tools = [];
                foreach (var toolName in toolNames)
                {
                    var normalizedToolName = toolName.Trim();
                    if (string.IsNullOrWhiteSpace(normalizedToolName))
                        continue;

                    var toolDelegate = processContext.ToolMethodRegistry.GetToolFromDisplayName(normalizedToolName);
                    if (toolDelegate is null)
                        continue;

                    tools.Add(AIFunctionFactory.Create(toolDelegate, normalizedToolName));
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
            ChatHistoryReplayHelper.AddPreparedInputMessages(chat, threadContext.NodeContext, node.ChatInputPath);
    }

    protected virtual async Task AddImageMessages(List<ChatMessage> chat, Model model, ModelConfig modelConfig, ProcessContext processContext, ThreadContext threadContext, ModelCallNodeEntity node)
    {
        if (HasCapability(model, modelConfig, "SupportsImageIn"))
        {
            if (!string.IsNullOrWhiteSpace(node.ImageInputPath))
            {
                if (!threadContext.NodeContext.TryGet<object?>(node.ImageInputPath, out var imageValue))
                    throw new SharpOMaticException($"Image input path '{node.ImageInputPath}' could not be resolved.");

                if (imageValue is null)
                    return;

                if (imageValue is AssetRef assetRef)
                    await AddAssetImageMessage(chat, processContext, assetRef);
                else if (imageValue is ContextList assetList)
                {
                    for (var i = 0; i < assetList.Count; i += 1)
                    {
                        var item = assetList[i];
                        if (item is AssetRef listAssetRef)
                            await AddAssetImageMessage(chat, processContext, listAssetRef);
                        else if (item is string listImageUrl)
                            chat.Add(CreateImageUriMessage(node.ImageInputPath, listImageUrl));
                        else
                            throw new SharpOMaticException($"Image input path '{node.ImageInputPath}' contains an entry at index {i} that is not an asset or image URL.");
                    }
                }
                else if (imageValue is string imageUrl)
                    chat.Add(CreateImageUriMessage(node.ImageInputPath, imageUrl));
                else
                    throw new SharpOMaticException($"Image input path '{node.ImageInputPath}' must be an asset, asset list, or image URL.");
            }
        }
    }

    private static async Task AddAssetImageMessage(List<ChatMessage> chat, ProcessContext processContext, AssetRef entry)
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

    private static ChatMessage CreateImageUriMessage(string imageInputPath, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new SharpOMaticException($"Image input path '{imageInputPath}' must be an asset, asset list, or image URL.");

        var mediaType = GetImageMediaType(uri);
        if (string.IsNullOrWhiteSpace(mediaType))
            throw new SharpOMaticException($"Image input URL '{uri}' must resolve to an image media type.");

        return new ChatMessage(ChatRole.User, [new UriContent(uri.ToString(), mediaType)]);
    }

    private static string? GetImageMediaType(Uri uri)
    {
        return Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() switch
        {
            ".avif" => "image/avif",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".jpeg" => "image/jpeg",
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            ".webp" => "image/webp",
            _ => null,
        };
    }

    protected virtual async Task<(string? instructions, string? prompt)> ResolveInstructionsAndPrompt(
        List<ChatMessage> chat,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node
    )
    {
        string? instructions = null;
        if (!string.IsNullOrWhiteSpace(node.Instructions))
        {
            instructions = await ContextHelpers.SubstituteValuesAsync(
                node.Instructions,
                threadContext.NodeContext,
                processContext.RepositoryService,
                processContext.AssetStore,
                processContext.Run.RunId,
                processContext.Run.ConversationId
            );
        }

        if (!string.IsNullOrWhiteSpace(node.Prompt))
        {
            var prompt = await ContextHelpers.SubstituteValuesAsync(
                node.Prompt,
                threadContext.NodeContext,
                processContext.RepositoryService,
                processContext.AssetStore,
                processContext.Run.RunId,
                processContext.Run.ConversationId
            );

            chat.Add(new ChatMessage(ChatRole.User, [new TextContent(prompt)]));
            return (instructions, prompt);
        }

        return (instructions, null);
    }

    protected virtual Task EmitPromptStreamEvents(ProcessContext processContext, string? prompt, bool disableStreamUser)
    {
        if (disableStreamUser || string.IsNullOrWhiteSpace(prompt))
            return Task.CompletedTask;

        var messageId = $"user-{Guid.NewGuid():N}";
        return processContext.AppendStreamEvents([
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextStart,
                MessageId = messageId,
                MessageRole = StreamMessageRole.User,
                Silent = true,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextContent,
                MessageId = messageId,
                TextDelta = prompt,
                Silent = true,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextEnd,
                MessageId = messageId,
                Silent = true,
            },
        ]);
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

    protected virtual object? ResponseToOutputValue(bool jsonOutput, AgentResponse response)
    {
        StringBuilder sb = new();
        foreach (var message in response.Messages)
            if (!string.IsNullOrEmpty(message.Text))
                sb.Append(message.Text);

        if (jsonOutput)
        {
            try
            {
                return FastDeserializeString(sb.ToString());
            }
            catch
            {
                throw new SharpOMaticException($"Model response could not be parsed as json.");
            }
        }

        return sb.ToString();
    }
}
