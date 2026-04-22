
namespace SharpOMatic.AGUI.Controllers;

[ApiController]
[Route("agent/agui")]
public sealed class AgUiController(IEngineService engineService, IAgUiRunEventBroker broker) : ControllerBase
{
    private const string ChatContextPath = "input.chat";
    private const string StateSyncContentPath = "agent._hidden.state";

    [HttpPost]
    public async Task Post([FromBody] AgUiRunRequest request)
    {
        ConfigureSseResponse(Response);

        var threadId = request.ThreadId?.Trim();
        var protocolRunId = NormalizeProtocolRunId(request.RunId);

        try
        {
            if (string.IsNullOrWhiteSpace(threadId))
                throw new SharpOMaticException("AG-UI threadId is required.");

            var workflowTarget = await ResolveWorkflowTarget(request);
            var engineRunId = await StartAgUiRun(request, threadId, workflowTarget);

            await WriteEventAsync(new
            {
                type = "RUN_STARTED",
                threadId,
                runId = protocolRunId
            });

            await foreach (var update in broker.Subscribe(engineRunId, HttpContext.RequestAborted))
            {
                if (update.StreamEvents is not null)
                {
                    foreach (var streamEvent in update.StreamEvents)
                        await WriteStreamEventAsync(streamEvent);
                }

                if (update.Run is null)
                    continue;

                if (update.Run.RunStatus is RunStatus.Success or RunStatus.Suspended)
                {
                    await WriteEventAsync(new
                    {
                        type = "RUN_FINISHED",
                        threadId,
                        runId = protocolRunId
                    });
                    break;
                }

                if (update.Run.RunStatus == RunStatus.Failed)
                {
                    await WriteEventAsync(new
                    {
                        type = "RUN_ERROR",
                        threadId,
                        runId = protocolRunId,
                        message = update.Run.Error ?? "Workflow failed."
                    });
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected.
        }
        catch (Exception ex)
        {
            await WriteEventAsync(new
            {
                type = "RUN_ERROR",
                threadId,
                runId = protocolRunId,
                message = ex.Message
            });
        }
    }

    private async Task<Guid> StartAgUiRun(AgUiRunRequest request, string threadId, ResolvedWorkflowTarget workflowTarget)
    {
        if (!workflowTarget.IsConversationEnabled)
        {
            var rootContext = BuildBaseContext(request);
            return await engineService.StartWorkflowRunAndNotify(workflowTarget.WorkflowId, rootContext);
        }

        var repositoryService = HttpContext?.RequestServices?.GetService<IRepositoryService>();
        if (repositoryService is null)
        {
            var resumeInput = new AgUiAgentResumeInput() { Agent = BuildAgentContext(request) };
            return await engineService.StartOrResumeConversationAndNotify(
                workflowTarget.WorkflowId,
                threadId,
                resumeInput: resumeInput,
                streamConversationId: threadId
            );
        }

        var mergedContext = await BuildConversationContextAsync(request, threadId, repositoryService);
        return await engineService.StartOrResumeConversationAndNotify(
            workflowTarget.WorkflowId,
            threadId,
            resumeInput: new ContextMergeResumeInput() { Context = mergedContext },
            streamConversationId: threadId
        );
    }

    private async Task<ResolvedWorkflowTarget> ResolveWorkflowTarget(AgUiRunRequest request)
    {
        var selector = request.ForwardedProps;
        if (!selector.HasValue || selector.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new SharpOMaticException("AG-UI forwardedProps must specify either workflowId or workflowName.");

        var workflowSelector = selector.Value;
        if (workflowSelector.ValueKind != JsonValueKind.Object)
            throw new SharpOMaticException("AG-UI forwardedProps must be a JSON object.");

        if (workflowSelector.TryGetProperty("sharpomatic", out var scopedSelector) && scopedSelector.ValueKind == JsonValueKind.Object)
            workflowSelector = scopedSelector;

        var workflowId = TryReadStringProperty(workflowSelector, "workflowId");
        var workflowName = TryReadStringProperty(workflowSelector, "workflowName");

        if (string.IsNullOrWhiteSpace(workflowId) == string.IsNullOrWhiteSpace(workflowName))
            throw new SharpOMaticException("Specify exactly one of workflowId or workflowName in AG-UI forwardedProps.");

        Guid resolvedWorkflowId;
        if (!string.IsNullOrWhiteSpace(workflowId))
        {
            if (!Guid.TryParse(workflowId, out var parsedWorkflowId))
                throw new SharpOMaticException("AG-UI workflowId must be a valid GUID.");

            resolvedWorkflowId = parsedWorkflowId;
        }
        else
            resolvedWorkflowId = await engineService.GetWorkflowId(workflowName!);

        var repositoryService = HttpContext?.RequestServices?.GetService<IRepositoryService>();
        if (repositoryService is null)
            return new ResolvedWorkflowTarget(resolvedWorkflowId, IsConversationEnabled: true);

        var workflow = await repositoryService.GetWorkflow(resolvedWorkflowId);
        return new ResolvedWorkflowTarget(resolvedWorkflowId, workflow.IsConversationEnabled);
    }

    private async Task<ContextObject> BuildConversationContextAsync(AgUiRunRequest request, string threadId, IRepositoryService repositoryService)
    {
        var checkpoint = await repositoryService.GetConversationCheckpoint(threadId);
        var mergedContext = checkpoint is not null && !string.IsNullOrWhiteSpace(checkpoint.ContextJson)
            ? ContextObject.Deserialize(checkpoint.ContextJson, HttpContext.RequestServices)
            : new ContextObject();

        ApplyAgUiContext(mergedContext, request);
        AppendConversationChatHistory(mergedContext, ConvertMessagesToChatHistory(request.Messages));
        return mergedContext;
    }

    private static ContextObject BuildBaseContext(AgUiRunRequest request)
    {
        var rootContext = new ContextObject();
        ApplyAgUiContext(rootContext, request);
        rootContext.Set(ChatContextPath, ToContextList(ConvertMessagesToChatHistory(request.Messages)));
        return rootContext;
    }

    private static void ApplyAgUiContext(ContextObject rootContext, AgUiRunRequest request)
    {
        rootContext["agent"] = BuildAgentContext(request);

        if (request.State.HasValue && request.State.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
            rootContext.Set(StateSyncContentPath, ContextHelpers.FastDeserializeString(request.State.Value.GetRawText()));
        else
            rootContext.RemovePath(StateSyncContentPath);
    }

    private static ContextObject BuildAgentContext(AgUiRunRequest request)
    {
        var agentPayload = new Dictionary<string, object?>();
        AddJsonProperty(agentPayload, "latestUserMessage", FindLatestUserMessage(request.Messages));
        AddJsonProperty(agentPayload, "latestToolResult", FindLatestToolResult(request.Messages));
        AddJsonProperty(agentPayload, "messages", request.Messages);
        AddJsonProperty(agentPayload, "state", request.State);
        AddJsonProperty(agentPayload, "context", request.Context);

        return ContextHelpers.FastDeserializeString(JsonSerializer.Serialize(agentPayload)) as ContextObject
            ?? throw new SharpOMaticException("AG-UI request payload could not be converted into workflow context.");
    }

    private static void AppendConversationChatHistory(ContextObject rootContext, List<ChatMessage> incomingMessages)
    {
        if (!TryReadChatHistory(rootContext, out var chatHistory))
        {
            rootContext.Set(ChatContextPath, ToContextList(incomingMessages));
            return;
        }

        if (incomingMessages.Count == 0)
            return;

        var existingIds = chatHistory.Messages
            .Where(message => !string.IsNullOrWhiteSpace(message.MessageId))
            .Select(message => message.MessageId!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var incomingMessage in incomingMessages)
        {
            if (string.IsNullOrWhiteSpace(incomingMessage.MessageId))
                continue;

            if (existingIds.Contains(incomingMessage.MessageId))
            {
                throw new SharpOMaticException(
                    $"Conversation-enabled AG-UI workflows must send only new messages after initialization. Message id '{incomingMessage.MessageId}' was already received."
                );
            }
        }

        foreach (var incomingMessage in incomingMessages)
            chatHistory.Storage.Add(incomingMessage);
    }

    private static bool TryReadChatHistory(ContextObject rootContext, out ChatHistory chatHistory)
    {
        chatHistory = default!;

        if (rootContext.TryGet<ContextList>(ChatContextPath, out var chatList) && chatList is not null)
        {
            List<ChatMessage> messages = [];
            foreach (var entry in chatList)
            {
                if (entry is not ChatMessage message)
                    throw new SharpOMaticException($"AG-UI expected '{ChatContextPath}' to contain only ChatMessage entries.");

                messages.Add(message);
            }

            chatHistory = new ChatHistory(chatList, messages);
            return true;
        }

        if (rootContext.TryGet<ChatMessage>(ChatContextPath, out var chatMessage) && chatMessage is not null)
        {
            ContextList chatStorage = [chatMessage];
            rootContext.Set(ChatContextPath, chatStorage);
            chatHistory = new ChatHistory(chatStorage, [chatMessage]);
            return true;
        }

        return false;
    }

    private static ContextList ToContextList(IEnumerable<ChatMessage> messages)
    {
        ContextList chat = [];
        foreach (var message in messages)
            chat.Add(message);

        return chat;
    }

    private static List<ChatMessage> ConvertMessagesToChatHistory(JsonElement? messages)
    {
        List<ChatMessage> chat = [];
        if (!messages.HasValue || messages.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return chat;

        if (messages.Value.ValueKind != JsonValueKind.Array)
            throw new SharpOMaticException("AG-UI messages must be a JSON array.");

        foreach (var message in messages.Value.EnumerateArray())
        {
            if (message.ValueKind != JsonValueKind.Object)
                throw new SharpOMaticException("AG-UI messages must contain JSON objects.");

            var role = ReadRequiredIdentifier(message, "role", "AG-UI message role must be a non-empty string.").ToLowerInvariant();
            switch (role)
            {
                case "system":
                    chat.Add(CreateTextOnlyMessage(message, ChatRole.System));
                    break;
                case "developer":
                    chat.Add(CreateTextOnlyMessage(message, ChatRole.System));
                    break;
                case "user":
                    chat.Add(CreateTextOnlyMessage(message, ChatRole.User));
                    break;
                case "assistant":
                    var assistantMessage = CreateAssistantMessage(message);
                    if (assistantMessage is not null)
                        chat.Add(assistantMessage);
                    break;
                case "tool":
                    chat.Add(CreateToolResultMessage(message));
                    break;
                case "reasoning":
                case "activity":
                    break;
                default:
                    throw new SharpOMaticException($"AG-UI message role '{role}' is not supported.");
            }
        }

        return chat;
    }

    private static ChatMessage CreateTextOnlyMessage(JsonElement message, ChatRole role)
    {
        var messageId = ReadRequiredIdentifier(message, "id", "AG-UI message id must be a non-empty string.");
        var content = ReadRequiredStringContent(message, "content", $"AG-UI {message.GetProperty("role").GetString()} message content must be a string.");

        return new ChatMessage(role, [new TextContent(content)])
        {
            MessageId = messageId,
            AuthorName = TryReadStringProperty(message, "name"),
        };
    }

    private static ChatMessage? CreateAssistantMessage(JsonElement message)
    {
        var messageId = ReadRequiredIdentifier(message, "id", "AG-UI message id must be a non-empty string.");
        List<AIContent> contents = [];

        if (message.TryGetProperty("content", out var content))
        {
            if (content.ValueKind != JsonValueKind.String)
                throw new SharpOMaticException("AG-UI assistant message content must be a string in this version.");

            var contentText = content.GetString();
            if (!string.IsNullOrWhiteSpace(contentText))
                contents.Add(new TextContent(contentText));
        }

        if (message.TryGetProperty("toolCalls", out var toolCalls))
        {
            if (toolCalls.ValueKind != JsonValueKind.Array)
                throw new SharpOMaticException("AG-UI assistant toolCalls must be a JSON array.");

            foreach (var toolCall in toolCalls.EnumerateArray())
                contents.Add(CreateFunctionCallContent(toolCall));
        }

        if (contents.Count == 0)
            return null;

        return new ChatMessage(ChatRole.Assistant, contents)
        {
            MessageId = messageId,
            AuthorName = TryReadStringProperty(message, "name"),
        };
    }

    private static FunctionCallContent CreateFunctionCallContent(JsonElement toolCall)
    {
        if (toolCall.ValueKind != JsonValueKind.Object)
            throw new SharpOMaticException("AG-UI assistant toolCalls entries must be JSON objects.");

        var toolCallId = ReadRequiredIdentifier(toolCall, "id", "AG-UI assistant tool call id must be a non-empty string.");
        var toolCallType = TryReadStringProperty(toolCall, "type");
        if (!string.IsNullOrWhiteSpace(toolCallType) && !string.Equals(toolCallType, "function", StringComparison.OrdinalIgnoreCase))
            throw new SharpOMaticException($"AG-UI assistant tool call type '{toolCallType}' is not supported.");

        if (!toolCall.TryGetProperty("function", out var function) || function.ValueKind != JsonValueKind.Object)
            throw new SharpOMaticException("AG-UI assistant tool calls must include a function object.");

        var functionName = ReadRequiredIdentifier(function, "name", "AG-UI assistant tool call function name must be a non-empty string.");
        var argumentsJson = ReadRequiredStringContent(function, "arguments", "AG-UI assistant tool call arguments must be a JSON string.");
        return new FunctionCallContent(toolCallId, functionName, ParseToolArguments(argumentsJson));
    }

    private static IDictionary<string, object?> ParseToolArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return new Dictionary<string, object?>();

        try
        {
            var parsed = ContextHelpers.FastDeserializeString(argumentsJson);
            if (parsed is not ContextObject contextObject)
                throw new SharpOMaticException("AG-UI assistant tool call arguments must decode to a JSON object.");

            Dictionary<string, object?> arguments = [];
            foreach (var entry in contextObject)
                arguments[entry.Key] = entry.Value;

            return arguments;
        }
        catch (SharpOMaticException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new SharpOMaticException("AG-UI assistant tool call arguments must contain valid JSON.");
        }
    }

    private static ChatMessage CreateToolResultMessage(JsonElement message)
    {
        var messageId = ReadRequiredIdentifier(message, "id", "AG-UI message id must be a non-empty string.");
        var toolCallId = ReadRequiredIdentifier(message, "toolCallId", "AG-UI tool messages must include a non-empty toolCallId.");
        var content = ReadRequiredStringContent(message, "content", "AG-UI tool message content must be a string.");

        return new ChatMessage(ChatRole.Tool, [new FunctionResultContent(toolCallId, content)])
        {
            MessageId = messageId,
            AuthorName = TryReadStringProperty(message, "name"),
        };
    }

    private static string ReadRequiredIdentifier(JsonElement element, string propertyName, string errorMessage)
    {
        var value = TryReadStringProperty(element, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        throw new SharpOMaticException(errorMessage);
    }

    private static string ReadRequiredStringContent(JsonElement element, string propertyName, string errorMessage)
    {
        if (element.TryGetProperty(propertyName, out var propertyValue) && propertyValue.ValueKind != JsonValueKind.String)
        {
            throw new SharpOMaticException(
                propertyName switch
                {
                    "content" => errorMessage.Contains("assistant", StringComparison.OrdinalIgnoreCase)
                        ? "AG-UI assistant message content must be a string in this version."
                        : errorMessage,
                    _ => errorMessage
                }
            );
        }

        if (!element.TryGetProperty(propertyName, out var stringProperty))
            throw new SharpOMaticException(errorMessage);

        return stringProperty.GetString() ?? string.Empty;
    }

    private async Task WriteStreamEventAsync(StreamEventProgressItem streamEventUpdate)
    {
        if (streamEventUpdate.Silent)
            return;

        var streamEvent = streamEventUpdate.Event;
        var toolCallId = string.IsNullOrWhiteSpace(streamEvent.ToolCallId) ? streamEvent.MessageId : streamEvent.ToolCallId;
        var reasoningMessageId = GetProtocolReasoningMessageId(streamEvent.MessageId);
        var toolMessageId = GetProtocolToolMessageId(streamEvent.MessageId);
        var activityMessageId = GetProtocolActivityMessageId(streamEvent.MessageId);

        switch (streamEvent.EventKind)
        {
            case StreamEventKind.TextStart when !string.IsNullOrWhiteSpace(streamEvent.MessageId):
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_START",
                    messageId = streamEvent.MessageId,
                    role = MapRole(streamEvent.MessageRole)
                });
                break;
            case StreamEventKind.TextContent when !string.IsNullOrWhiteSpace(streamEvent.MessageId):
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_CONTENT",
                    messageId = streamEvent.MessageId,
                    delta = streamEvent.TextDelta ?? string.Empty
                });
                break;
            case StreamEventKind.TextEnd when !string.IsNullOrWhiteSpace(streamEvent.MessageId):
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId = streamEvent.MessageId
                });
                break;
            case StreamEventKind.ReasoningStart when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_START",
                    messageId = reasoningMessageId
                });
                break;
            case StreamEventKind.ReasoningMessageStart when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_MESSAGE_START",
                    messageId = reasoningMessageId,
                    role = MapRole(streamEvent.MessageRole)
                });
                break;
            case StreamEventKind.ReasoningMessageContent when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_MESSAGE_CONTENT",
                    messageId = reasoningMessageId,
                    delta = streamEvent.TextDelta ?? string.Empty
                });
                break;
            case StreamEventKind.ReasoningMessageEnd when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_MESSAGE_END",
                    messageId = reasoningMessageId
                });
                break;
            case StreamEventKind.ReasoningEnd when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_END",
                    messageId = reasoningMessageId
                });
                break;
            case StreamEventKind.ToolCallStart when !string.IsNullOrWhiteSpace(toolCallId):
                await WriteEventAsync(new
                {
                    type = "TOOL_CALL_START",
                    toolCallId,
                    toolCallName = streamEvent.TextDelta ?? string.Empty,
                    parentMessageId = streamEvent.ParentMessageId
                });
                break;
            case StreamEventKind.ToolCallArgs when !string.IsNullOrWhiteSpace(toolCallId):
                await WriteEventAsync(new
                {
                    type = "TOOL_CALL_ARGS",
                    toolCallId,
                    delta = streamEvent.TextDelta ?? string.Empty
                });
                break;
            case StreamEventKind.ToolCallEnd when !string.IsNullOrWhiteSpace(toolCallId):
                await WriteEventAsync(new
                {
                    type = "TOOL_CALL_END",
                    toolCallId
                });
                break;
            case StreamEventKind.ToolCallResult when toolMessageId is not null && !string.IsNullOrWhiteSpace(toolCallId):
                await WriteEventAsync(new
                {
                    type = "TOOL_CALL_RESULT",
                    messageId = toolMessageId,
                    toolCallId,
                    content = streamEvent.TextDelta ?? string.Empty,
                    role = "tool"
                });
                break;
            case StreamEventKind.ActivitySnapshot when activityMessageId is not null && !string.IsNullOrWhiteSpace(streamEvent.ActivityType):
                await WriteActivitySnapshotEventAsync(activityMessageId, streamEvent);
                break;
            case StreamEventKind.ActivityDelta when activityMessageId is not null && !string.IsNullOrWhiteSpace(streamEvent.ActivityType):
                await WriteActivityDeltaEventAsync(activityMessageId, streamEvent);
                break;
            case StreamEventKind.StateSnapshot:
                await WriteStateSnapshotEventAsync(streamEvent);
                break;
            case StreamEventKind.StateDelta:
                await WriteStateDeltaEventAsync(streamEvent);
                break;
            case StreamEventKind.Custom when !string.IsNullOrWhiteSpace(streamEvent.TextDelta):
                await WriteEventAsync(new
                {
                    type = "CUSTOM",
                    name = streamEvent.TextDelta,
                    value = streamEvent.Metadata ?? string.Empty
                });
                break;
            case StreamEventKind.StepStart when !string.IsNullOrWhiteSpace(streamEvent.TextDelta):
                await WriteEventAsync(new
                {
                    type = "STEP_STARTED",
                    stepName = streamEvent.TextDelta
                });
                break;
            case StreamEventKind.StepEnd when !string.IsNullOrWhiteSpace(streamEvent.TextDelta):
                await WriteEventAsync(new
                {
                    type = "STEP_FINISHED",
                    stepName = streamEvent.TextDelta
                });
                break;
        }
    }

    private static string? GetProtocolReasoningMessageId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        return $"reason:{messageId.Trim()}";
    }

    private static string? GetProtocolToolMessageId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        return $"tool:{messageId.Trim()}";
    }

    private static string? GetProtocolActivityMessageId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        return $"activity:{messageId.Trim()}";
    }

    private static void AddJsonProperty(IDictionary<string, object?> payload, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            payload[propertyName] = value.Trim();
    }

    private static void AddJsonProperty(IDictionary<string, object?> payload, string propertyName, JsonElement? value)
    {
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return;

        payload[propertyName] = value.Value;
    }

    private static void AddJsonProperty(IDictionary<string, object?> payload, string propertyName, object? value)
    {
        if (value is null)
            return;

        payload[propertyName] = value;
    }

    private static JsonElement? FindLatestUserMessage(JsonElement? messages)
    {
        var latestMessage = GetLatestMessage(messages);
        if (!latestMessage.HasValue)
            return null;

        var message = latestMessage.Value;
        if (!HasRole(message, "user"))
            return null;

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            return null;

        return message;
    }

    private static object? FindLatestToolResult(JsonElement? messages)
    {
        var latestMessage = GetLatestMessage(messages);
        if (!latestMessage.HasValue)
            return null;

        if (!HasRole(latestMessage.Value, "tool"))
            return null;

        return ConvertLatestToolResult(latestMessage.Value);
    }

    private static Dictionary<string, object?> ConvertLatestToolResult(JsonElement message)
    {
        Dictionary<string, object?> payload = [];

        foreach (var property in message.EnumerateObject())
        {
            payload[property.Name] = property.Value;
        }

        if (message.TryGetProperty("content", out var content))
            AddLatestToolResultValue(payload, content);

        return payload;
    }

    private static void AddLatestToolResultValue(IDictionary<string, object?> payload, JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.String)
            return;

        var rawContent = content.GetString();
        if (string.IsNullOrWhiteSpace(rawContent))
            return;

        try
        {
            payload["value"] = ContextHelpers.FastDeserializeString(rawContent);
        }
        catch
        {
            // Preserve plain text tool results as raw strings only.
        }
    }

    private static JsonElement? GetLatestMessage(JsonElement? messages)
    {
        if (!messages.HasValue || messages.Value.ValueKind != JsonValueKind.Array)
            return null;

        JsonElement? latestMessage = null;
        foreach (var message in messages.Value.EnumerateArray())
            latestMessage = message;

        if (!latestMessage.HasValue || latestMessage.Value.ValueKind != JsonValueKind.Object)
            return null;

        return latestMessage;
    }

    private static bool HasRole(JsonElement message, string expectedRole)
    {
        if (!message.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String)
            return false;

        return string.Equals(role.GetString(), expectedRole, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProtocolRunId(string? runId)
    {
        return string.IsNullOrWhiteSpace(runId)
            ? Guid.NewGuid().ToString("N")
            : runId.Trim();
    }

    private static string? TryReadStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyValue) || propertyValue.ValueKind != JsonValueKind.String)
            return null;

        return propertyValue.GetString()?.Trim();
    }

    private async Task WriteActivitySnapshotEventAsync(string activityMessageId, StreamEvent streamEvent)
    {
        var content = ParseJsonPayload(streamEvent.TextDelta, "ActivitySnapshot");
        if (content.ValueKind != JsonValueKind.Object)
            throw new SharpOMaticException("ActivitySnapshot stream event payload must be a JSON object.");

        await WriteEventAsync(
            streamEvent.Replace.HasValue
                ? new
                {
                    type = "ACTIVITY_SNAPSHOT",
                    messageId = activityMessageId,
                    activityType = streamEvent.ActivityType!,
                    content,
                    replace = streamEvent.Replace.Value
                }
                : new
                {
                    type = "ACTIVITY_SNAPSHOT",
                    messageId = activityMessageId,
                    activityType = streamEvent.ActivityType!,
                    content
                }
        );
    }

    private async Task WriteActivityDeltaEventAsync(string activityMessageId, StreamEvent streamEvent)
    {
        var patch = ParseJsonPayload(streamEvent.TextDelta, "ActivityDelta");
        if (patch.ValueKind != JsonValueKind.Array)
            throw new SharpOMaticException("ActivityDelta stream event payload must be a JSON array.");

        await WriteEventAsync(new
        {
            type = "ACTIVITY_DELTA",
            messageId = activityMessageId,
            activityType = streamEvent.ActivityType!,
            patch
        });
    }

    private async Task WriteStateSnapshotEventAsync(StreamEvent streamEvent)
    {
        var snapshot = ParseJsonPayload(streamEvent.TextDelta, "StateSnapshot");

        await WriteEventAsync(new
        {
            type = "STATE_SNAPSHOT",
            snapshot
        });
    }

    private async Task WriteStateDeltaEventAsync(StreamEvent streamEvent)
    {
        var delta = ParseJsonPayload(streamEvent.TextDelta, "StateDelta");
        if (delta.ValueKind != JsonValueKind.Array)
            throw new SharpOMaticException("StateDelta stream event payload must be a JSON array.");

        await WriteEventAsync(new
        {
            type = "STATE_DELTA",
            delta
        });
    }

    private static JsonElement ParseJsonPayload(string? payload, string eventName)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new SharpOMaticException($"{eventName} stream event payload cannot be empty.");

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new SharpOMaticException($"{eventName} stream event payload must contain valid JSON.");
        }
    }

    private static string MapRole(StreamMessageRole? role)
    {
        return role switch
        {
            StreamMessageRole.User => "user",
            StreamMessageRole.Developer => "developer",
            StreamMessageRole.System => "system",
            StreamMessageRole.Tool => "tool",
            StreamMessageRole.Reasoning => "reasoning",
            _ => "assistant",
        };
    }

    private static void ConfigureSseResponse(HttpResponse response)
    {
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("X-Accel-Buffering", "no");
        response.ContentType = "text/event-stream";
    }

    private async Task WriteEventAsync(object payload)
    {
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n");
        await Response.Body.FlushAsync(HttpContext.RequestAborted);
    }

    private sealed record ResolvedWorkflowTarget(Guid WorkflowId, bool IsConversationEnabled);

    private sealed record ChatHistory(ContextList Storage, List<ChatMessage> Messages);
}
