namespace SharpOMatic.AGUI.Services;

internal static class AgUiMessageHistoryBuilder
{
    private const string FrontendToolCallMessagePrefix = "frontend-tool-call:";

    public static List<Dictionary<string, object?>> Build(IEnumerable<StreamEvent> streamEvents)
    {
        return BuildMessages(streamEvents);
    }

    public static AgUiHistoryResponse BuildEnvelope(
        IEnumerable<StreamEvent> streamEvents,
        ConversationCheckpoint? checkpoint,
        IJsonConverterService jsonConverterService
    )
    {
        var orderedEvents = streamEvents.OrderBy(e => e.SequenceNumber).ThenBy(e => e.Created).ToList();
        var messages = BuildMessages(orderedEvents);
        var state = TryGetCheckpointState(checkpoint, jsonConverterService) ?? BuildState(orderedEvents);

        return new AgUiHistoryResponse()
        {
            Messages = messages,
            State = state,
            PendingFrontendTools = GetPendingFrontendTools(messages),
        };
    }

    public static async Task<AgUiHistoryResponse> BuildCappedEnvelope(
        string conversationId,
        int maxMessages,
        IRepositoryService repositoryService,
        ConversationCheckpoint? checkpoint,
        IJsonConverterService jsonConverterService
    )
    {
        var cappedMessages = await LoadCappedMessages(conversationId, maxMessages, repositoryService);
        var state = TryGetCheckpointState(checkpoint, jsonConverterService);
        if (state is null)
            state = BuildState(await repositoryService.GetConversationStateStreamEvents(conversationId));

        return new AgUiHistoryResponse()
        {
            Messages = cappedMessages,
            State = state,
            PendingFrontendTools = GetPendingFrontendTools(cappedMessages),
        };
    }

    private static async Task<List<Dictionary<string, object?>>> LoadCappedMessages(string conversationId, int maxMessages, IRepositoryService repositoryService)
    {
        var pageSize = maxMessages > int.MaxValue / 10 ? int.MaxValue : Math.Max(maxMessages * 10, 100);
        int? beforeSequenceNumber = null;
        Dictionary<string, HistoryCandidate> candidates = [];

        while (candidates.Count < maxMessages)
        {
            var page = await repositoryService.GetConversationStreamEventTail(conversationId, beforeSequenceNumber, pageSize);
            if (page.Count == 0)
                break;

            foreach (var streamEvent in page)
                AddHistoryCandidate(candidates, streamEvent);

            beforeSequenceNumber = page.Min(e => e.SequenceNumber);
        }

        if (candidates.Count == 0)
            return [];

        var selectedCandidates = candidates.Values.OrderByDescending(c => c.LatestSequenceNumber).Take(maxMessages).ToList();
        HashSet<string> messageIds = new(StringComparer.Ordinal);
        HashSet<string> toolCallIds = new(StringComparer.Ordinal);
        HashSet<string> allowedMessageIds = new(StringComparer.Ordinal);
        HashSet<string> allowedToolCallIds = new(StringComparer.Ordinal);
        HashSet<string> allowedToolResultCallIds = new(StringComparer.Ordinal);

        foreach (var candidate in selectedCandidates)
        {
            AddIfNotEmpty(allowedMessageIds, candidate.ProtocolMessageId);

            switch (candidate.Kind)
            {
                case HistoryCandidateKind.Text:
                case HistoryCandidateKind.Reasoning:
                case HistoryCandidateKind.Activity:
                    AddIfNotEmpty(messageIds, candidate.MessageId);
                    break;
                case HistoryCandidateKind.AssistantToolCall:
                    AddIfNotEmpty(toolCallIds, candidate.ToolCallId);
                    AddIfNotEmpty(allowedToolCallIds, candidate.ToolCallId);
                    break;
                case HistoryCandidateKind.ToolResult:
                    AddIfNotEmpty(messageIds, candidate.MessageId);
                    AddIfNotEmpty(toolCallIds, candidate.ToolCallId);
                    AddIfNotEmpty(allowedToolCallIds, candidate.ToolCallId);
                    AddIfNotEmpty(allowedToolResultCallIds, candidate.ToolCallId);
                    break;
            }
        }

        var targetedEvents = await LoadTargetedEvents(conversationId, repositoryService, messageIds, toolCallIds);

        var parentMessageIds = targetedEvents
            .Where(e => e.EventKind == StreamEventKind.ToolCallStart && IsInSet(toolCallIds, GetToolCallId(e)) && !string.IsNullOrWhiteSpace(e.ParentMessageId))
            .Select(e => e.ParentMessageId!)
            .Where(parentMessageId => !messageIds.Contains(parentMessageId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var parentMessageId in parentMessageIds)
        {
            messageIds.Add(parentMessageId);
            allowedMessageIds.Add(parentMessageId);
        }

        if (parentMessageIds.Count > 0)
            targetedEvents = await LoadTargetedEvents(conversationId, repositoryService, messageIds, toolCallIds);

        var knownToolCallIds = targetedEvents
            .Where(e => e.EventKind == StreamEventKind.ToolCallStart)
            .Select(GetToolCallId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        var orderedEvents = targetedEvents
            .GroupBy(e => e.StreamEventId)
            .Select(g => g.First())
            .OrderBy(e => e.SequenceNumber)
            .ThenBy(e => e.Created)
            .ToList();

        var messages = BuildMessages(orderedEvents);
        return FilterCappedMessages(messages, allowedMessageIds, allowedToolCallIds, allowedToolResultCallIds, knownToolCallIds);
    }

    private static async Task<List<StreamEvent>> LoadTargetedEvents(
        string conversationId,
        IRepositoryService repositoryService,
        HashSet<string> messageIds,
        HashSet<string> toolCallIds
    )
    {
        var byMessageId = await repositoryService.GetConversationStreamEventsByMessageIds(conversationId, messageIds.ToList());
        var byToolCallId = await repositoryService.GetConversationStreamEventsByToolCallIds(conversationId, toolCallIds.ToList());
        return byMessageId.Concat(byToolCallId).ToList();
    }

    private static void AddHistoryCandidate(Dictionary<string, HistoryCandidate> candidates, StreamEvent streamEvent)
    {
        switch (streamEvent.EventKind)
        {
            case StreamEventKind.TextStart:
            case StreamEventKind.TextContent:
            case StreamEventKind.TextEnd:
                AddMessageCandidate(candidates, streamEvent, HistoryCandidateKind.Text, streamEvent.MessageId, streamEvent.MessageId);
                break;
            case StreamEventKind.ReasoningStart:
            case StreamEventKind.ReasoningMessageStart:
            case StreamEventKind.ReasoningMessageContent:
            case StreamEventKind.ReasoningMessageEnd:
            case StreamEventKind.ReasoningEnd:
                if (!string.IsNullOrWhiteSpace(streamEvent.MessageId))
                    AddMessageCandidate(candidates, streamEvent, HistoryCandidateKind.Reasoning, streamEvent.MessageId, GetProtocolReasoningMessageId(streamEvent.MessageId));
                break;
            case StreamEventKind.ActivitySnapshot:
            case StreamEventKind.ActivityDelta:
                if (!string.IsNullOrWhiteSpace(streamEvent.MessageId))
                    AddMessageCandidate(candidates, streamEvent, HistoryCandidateKind.Activity, streamEvent.MessageId, GetProtocolActivityMessageId(streamEvent.MessageId));
                break;
            case StreamEventKind.ToolCallStart:
            case StreamEventKind.ToolCallArgs:
            case StreamEventKind.ToolCallEnd:
                AddToolCallCandidate(candidates, streamEvent);
                break;
            case StreamEventKind.ToolCallResult:
                AddToolResultCandidate(candidates, streamEvent);
                break;
        }
    }

    private static void AddMessageCandidate(
        Dictionary<string, HistoryCandidate> candidates,
        StreamEvent streamEvent,
        HistoryCandidateKind kind,
        string? messageId,
        string? protocolMessageId
    )
    {
        if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(protocolMessageId))
            return;

        UpsertCandidate(
            candidates,
            $"{kind}:{protocolMessageId}",
            kind,
            messageId,
            protocolMessageId,
            toolCallId: null,
            streamEvent.SequenceNumber
        );
    }

    private static void AddToolCallCandidate(Dictionary<string, HistoryCandidate> candidates, StreamEvent streamEvent)
    {
        var toolCallId = GetToolCallId(streamEvent);
        if (string.IsNullOrWhiteSpace(toolCallId))
            return;

        var assistantMessageId = string.IsNullOrWhiteSpace(streamEvent.ParentMessageId) ? toolCallId : streamEvent.ParentMessageId;
        UpsertCandidate(
            candidates,
            $"{HistoryCandidateKind.AssistantToolCall}:{assistantMessageId}:{toolCallId}",
            HistoryCandidateKind.AssistantToolCall,
            messageId: null,
            protocolMessageId: assistantMessageId,
            toolCallId,
            streamEvent.SequenceNumber
        );
    }

    private static void AddToolResultCandidate(Dictionary<string, HistoryCandidate> candidates, StreamEvent streamEvent)
    {
        var toolCallId = GetToolCallId(streamEvent);
        if (string.IsNullOrWhiteSpace(streamEvent.MessageId) || string.IsNullOrWhiteSpace(toolCallId))
            return;

        candidates[$"{HistoryCandidateKind.ToolResult}:{streamEvent.StreamEventId}"] = new HistoryCandidate(
            Kind: HistoryCandidateKind.ToolResult,
            MessageId: streamEvent.MessageId,
            ProtocolMessageId: null,
            ToolCallId: toolCallId,
            LatestSequenceNumber: streamEvent.SequenceNumber
        );
    }

    private static void UpsertCandidate(
        Dictionary<string, HistoryCandidate> candidates,
        string key,
        HistoryCandidateKind kind,
        string? messageId,
        string? protocolMessageId,
        string? toolCallId,
        int sequenceNumber
    )
    {
        if (candidates.TryGetValue(key, out var existing))
        {
            if (sequenceNumber > existing.LatestSequenceNumber)
                candidates[key] = existing with { LatestSequenceNumber = sequenceNumber };

            return;
        }

        candidates[key] = new HistoryCandidate(kind, messageId, protocolMessageId, toolCallId, sequenceNumber);
    }

    private static List<Dictionary<string, object?>> FilterCappedMessages(
        List<Dictionary<string, object?>> messages,
        HashSet<string> allowedMessageIds,
        HashSet<string> allowedToolCallIds,
        HashSet<string> allowedToolResultCallIds,
        HashSet<string> knownToolCallIds
    )
    {
        List<Dictionary<string, object?>> filteredMessages = [];

        foreach (var message in messages)
        {
            var role = message.GetValueOrDefault("role") as string;
            var messageId = message.GetValueOrDefault("id") as string;

            if (role == "tool")
            {
                var toolCallId = message.GetValueOrDefault("toolCallId") as string;
                if (IsInSet(allowedToolResultCallIds, toolCallId) && IsInSet(knownToolCallIds, toolCallId))
                    filteredMessages.Add(message);

                continue;
            }

            if (IsInSet(allowedMessageIds, messageId) || HasAllowedToolCall(message, allowedToolCallIds, knownToolCallIds))
                filteredMessages.Add(message);
        }

        return filteredMessages;
    }

    private static bool HasAllowedToolCall(Dictionary<string, object?> message, HashSet<string> allowedToolCallIds, HashSet<string> knownToolCallIds)
    {
        if (message.GetValueOrDefault("toolCalls") is not List<Dictionary<string, object?>> toolCalls)
            return false;

        return toolCalls.Any(toolCall =>
        {
            var toolCallId = toolCall.GetValueOrDefault("id") as string;
            return IsInSet(allowedToolCallIds, toolCallId) && IsInSet(knownToolCallIds, toolCallId);
        });
    }

    private static bool IsInSet(HashSet<string> values, string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && values.Contains(value);
    }

    private static void AddIfNotEmpty(HashSet<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value);
    }

    private static List<Dictionary<string, object?>> BuildMessages(IEnumerable<StreamEvent> streamEvents)
    {
        List<Dictionary<string, object?>> messages = [];
        Dictionary<string, Dictionary<string, object?>> messagesById = [];
        Dictionary<string, Dictionary<string, object?>> toolCallsById = [];

        foreach (var streamEvent in streamEvents.OrderBy(e => e.SequenceNumber).ThenBy(e => e.Created))
        {
            switch (streamEvent.EventKind)
            {
                case StreamEventKind.TextStart:
                    if (!string.IsNullOrWhiteSpace(streamEvent.MessageId))
                        EnsureTextMessage(messages, messagesById, streamEvent.MessageId, MapTextRole(streamEvent.MessageRole));
                    break;
                case StreamEventKind.TextContent:
                    if (!string.IsNullOrWhiteSpace(streamEvent.MessageId))
                        AppendTextContent(messages, messagesById, streamEvent.MessageId, streamEvent.TextDelta);
                    break;
                case StreamEventKind.ReasoningMessageStart:
                    if (!string.IsNullOrWhiteSpace(streamEvent.MessageId))
                        EnsureReasoningMessage(messages, messagesById, GetProtocolReasoningMessageId(streamEvent.MessageId));
                    break;
                case StreamEventKind.ReasoningMessageContent:
                    if (!string.IsNullOrWhiteSpace(streamEvent.MessageId))
                        AppendTextContent(messages, messagesById, GetProtocolReasoningMessageId(streamEvent.MessageId), streamEvent.TextDelta, "reasoning");
                    break;
                case StreamEventKind.ToolCallStart:
                    AddToolCallStart(messages, messagesById, toolCallsById, streamEvent);
                    break;
                case StreamEventKind.ToolCallArgs:
                    AddToolCallArgs(toolCallsById, streamEvent);
                    break;
                case StreamEventKind.ToolCallResult:
                    AddToolResult(messages, messagesById, streamEvent);
                    break;
                case StreamEventKind.ActivitySnapshot:
                    AddActivitySnapshot(messages, messagesById, streamEvent);
                    break;
                case StreamEventKind.ActivityDelta:
                    AddActivityDelta(messagesById, streamEvent);
                    break;
            }
        }

        return messages;
    }

    private static JsonNode? TryGetCheckpointState(ConversationCheckpoint? checkpoint, IJsonConverterService jsonConverterService)
    {
        if (string.IsNullOrWhiteSpace(checkpoint?.ContextJson))
            return null;

        var context = ContextObject.Deserialize(checkpoint.ContextJson, jsonConverterService);
        if (!context.TryGet<object?>("agent.state", out var state))
            return null;

        return ConvertContextValueToJsonNode(state, jsonConverterService);
    }

    private static JsonNode? ConvertContextValueToJsonNode(object? value, IJsonConverterService jsonConverterService)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonNode jsonNode:
                return jsonNode.DeepClone();
            case JsonElement jsonElement:
                return JsonNode.Parse(jsonElement.GetRawText());
            case ContextObject contextObject:
                var jsonObject = new JsonObject();
                foreach (var entry in contextObject)
                    jsonObject[entry.Key] = ConvertContextValueToJsonNode(entry.Value, jsonConverterService);

                return jsonObject;
            case ContextList contextList:
                var jsonArray = new JsonArray();
                foreach (var item in contextList)
                    jsonArray.Add(ConvertContextValueToJsonNode(item, jsonConverterService));

                return jsonArray;
            default:
                return JsonSerializer.SerializeToNode(value, new JsonSerializerOptions().BuildOptions(jsonConverterService.GetConverters()));
        }
    }

    private static JsonNode? BuildState(IEnumerable<StreamEvent> streamEvents)
    {
        JsonNode? state = null;

        foreach (var streamEvent in streamEvents)
        {
            switch (streamEvent.EventKind)
            {
                case StreamEventKind.StateSnapshot:
                    if (!string.IsNullOrWhiteSpace(streamEvent.TextDelta))
                        state = JsonNode.Parse(streamEvent.TextDelta);
                    break;
                case StreamEventKind.StateDelta:
                    if (state is not null && !string.IsNullOrWhiteSpace(streamEvent.TextDelta))
                    {
                        using var patchDocument = JsonDocument.Parse(streamEvent.TextDelta);
                        if (patchDocument.RootElement.ValueKind == JsonValueKind.Array)
                            ApplyPatch(state, patchDocument.RootElement);
                    }
                    break;
            }
        }

        return state;
    }

    private static List<AgUiPendingFrontendTool> GetPendingFrontendTools(List<Dictionary<string, object?>> messages)
    {
        if (messages.Count == 0)
            return [];

        var lastMessage = messages[^1];
        if (lastMessage.GetValueOrDefault("role") as string != "assistant")
            return [];

        var assistantMessageId = lastMessage.GetValueOrDefault("id") as string;
        if (string.IsNullOrWhiteSpace(assistantMessageId) || !assistantMessageId.StartsWith(FrontendToolCallMessagePrefix, StringComparison.Ordinal))
            return [];

        var expectedToolCallId = assistantMessageId[FrontendToolCallMessagePrefix.Length..];
        if (string.IsNullOrWhiteSpace(expectedToolCallId) || HasToolResult(messages, expectedToolCallId))
            return [];

        if (lastMessage.GetValueOrDefault("toolCalls") is not List<Dictionary<string, object?>> toolCalls)
            return [];

        var toolCall = toolCalls.FirstOrDefault(candidate => string.Equals(candidate.GetValueOrDefault("id") as string, expectedToolCallId, StringComparison.Ordinal));
        if (toolCall is null || toolCall.GetValueOrDefault("function") is not Dictionary<string, object?> function)
            return [];

        return
        [
            new AgUiPendingFrontendTool()
            {
                ToolCallId = expectedToolCallId,
                ToolName = function.GetValueOrDefault("name") as string ?? string.Empty,
                ArgumentsJson = function.GetValueOrDefault("arguments") as string ?? string.Empty,
                AssistantMessageId = assistantMessageId,
            }
        ];
    }

    private static bool HasToolResult(List<Dictionary<string, object?>> messages, string toolCallId)
    {
        return messages.Any(message =>
            message.GetValueOrDefault("role") as string == "tool" &&
            string.Equals(message.GetValueOrDefault("toolCallId") as string, toolCallId, StringComparison.Ordinal)
        );
    }

    private static Dictionary<string, object?> EnsureTextMessage(List<Dictionary<string, object?>> messages, Dictionary<string, Dictionary<string, object?>> messagesById, string messageId, string role)
    {
        if (messagesById.TryGetValue(messageId, out var existing))
        {
            if (!existing.ContainsKey("content"))
                existing["content"] = string.Empty;

            return existing;
        }

        var message = new Dictionary<string, object?>()
        {
            ["id"] = messageId,
            ["role"] = role,
            ["content"] = string.Empty,
        };

        messages.Add(message);
        messagesById[messageId] = message;
        return message;
    }

    private static void EnsureReasoningMessage(List<Dictionary<string, object?>> messages, Dictionary<string, Dictionary<string, object?>> messagesById, string messageId)
    {
        EnsureTextMessage(messages, messagesById, messageId, "reasoning");
    }

    private static void AppendTextContent(List<Dictionary<string, object?>> messages, Dictionary<string, Dictionary<string, object?>> messagesById, string messageId, string? delta, string defaultRole = "assistant")
    {
        var message = EnsureTextMessage(messages, messagesById, messageId, defaultRole);
        message["content"] = $"{message.GetValueOrDefault("content") as string ?? string.Empty}{delta ?? string.Empty}";
    }

    private static void AddToolCallStart(
        List<Dictionary<string, object?>> messages,
        Dictionary<string, Dictionary<string, object?>> messagesById,
        Dictionary<string, Dictionary<string, object?>> toolCallsById,
        StreamEvent streamEvent
    )
    {
        var toolCallId = GetToolCallId(streamEvent);
        if (string.IsNullOrWhiteSpace(toolCallId))
            return;

        var assistantMessage = ResolveToolCallAssistantMessage(messages, messagesById, toolCallId, streamEvent.ParentMessageId);
        var toolCalls = GetOrCreateToolCalls(assistantMessage);

        var toolCall = new Dictionary<string, object?>()
        {
            ["id"] = toolCallId,
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>()
            {
                ["name"] = streamEvent.TextDelta ?? string.Empty,
                ["arguments"] = string.Empty,
            },
        };

        toolCalls.Add(toolCall);
        toolCallsById[toolCallId] = toolCall;
    }

    private static void AddToolCallArgs(Dictionary<string, Dictionary<string, object?>> toolCallsById, StreamEvent streamEvent)
    {
        var toolCallId = GetToolCallId(streamEvent);
        if (string.IsNullOrWhiteSpace(toolCallId) || !toolCallsById.TryGetValue(toolCallId, out var toolCall))
            return;

        if (toolCall.GetValueOrDefault("function") is not Dictionary<string, object?> function)
            return;

        function["arguments"] = $"{function.GetValueOrDefault("arguments") as string ?? string.Empty}{streamEvent.TextDelta ?? string.Empty}";
    }

    private static void AddToolResult(List<Dictionary<string, object?>> messages, Dictionary<string, Dictionary<string, object?>> messagesById, StreamEvent streamEvent)
    {
        var toolCallId = GetToolCallId(streamEvent);
        if (string.IsNullOrWhiteSpace(streamEvent.MessageId) || string.IsNullOrWhiteSpace(toolCallId))
            return;

        var messageId = ResolveToolResultMessageId(messagesById, streamEvent.MessageId, toolCallId, streamEvent.SequenceNumber);
        if (messageId is null)
            return;

        var message = new Dictionary<string, object?>()
        {
            ["id"] = messageId,
            ["role"] = "tool",
            ["toolCallId"] = toolCallId,
            ["content"] = streamEvent.TextDelta ?? string.Empty,
        };

        messages.Add(message);
        messagesById[messageId] = message;
    }

    private static string? ResolveToolResultMessageId(Dictionary<string, Dictionary<string, object?>> messagesById, string messageId, string toolCallId, long sequenceNumber)
    {
        var protocolMessageId = GetProtocolToolMessageId(messageId);
        if (!messagesById.TryGetValue(protocolMessageId, out var existing))
            return protocolMessageId;

        if (existing.GetValueOrDefault("role") as string == "tool" && string.Equals(existing.GetValueOrDefault("toolCallId") as string, toolCallId, StringComparison.Ordinal))
            return null;

        var toolCallScopedMessageId = GetProtocolToolMessageId(messageId, toolCallId);
        if (!messagesById.ContainsKey(toolCallScopedMessageId))
            return toolCallScopedMessageId;

        return $"{toolCallScopedMessageId}:{sequenceNumber}";
    }

    private static void AddActivitySnapshot(List<Dictionary<string, object?>> messages, Dictionary<string, Dictionary<string, object?>> messagesById, StreamEvent streamEvent)
    {
        if (string.IsNullOrWhiteSpace(streamEvent.MessageId) || string.IsNullOrWhiteSpace(streamEvent.ActivityType) || string.IsNullOrWhiteSpace(streamEvent.TextDelta))
            return;

        var messageId = GetProtocolActivityMessageId(streamEvent.MessageId);
        var content = JsonNode.Parse(streamEvent.TextDelta);
        if (content is not JsonObject)
            return;

        if (messagesById.TryGetValue(messageId, out var existing))
        {
            if (existing.GetValueOrDefault("role") as string == "activity" && streamEvent.Replace != false)
            {
                existing["activityType"] = streamEvent.ActivityType;
                existing["content"] = content;
            }

            return;
        }

        var message = new Dictionary<string, object?>()
        {
            ["id"] = messageId,
            ["role"] = "activity",
            ["activityType"] = streamEvent.ActivityType,
            ["content"] = content,
        };

        messages.Add(message);
        messagesById[messageId] = message;
    }

    private static void AddActivityDelta(Dictionary<string, Dictionary<string, object?>> messagesById, StreamEvent streamEvent)
    {
        if (string.IsNullOrWhiteSpace(streamEvent.MessageId) || string.IsNullOrWhiteSpace(streamEvent.ActivityType) || string.IsNullOrWhiteSpace(streamEvent.TextDelta))
            return;

        var messageId = GetProtocolActivityMessageId(streamEvent.MessageId);
        if (!messagesById.TryGetValue(messageId, out var message) || message.GetValueOrDefault("content") is not JsonNode content)
            return;

        using var patchDocument = JsonDocument.Parse(streamEvent.TextDelta);
        if (patchDocument.RootElement.ValueKind != JsonValueKind.Array)
            return;

        ApplyPatch(content, patchDocument.RootElement);
        message["activityType"] = streamEvent.ActivityType;
    }

    private static Dictionary<string, object?> ResolveToolCallAssistantMessage(
        List<Dictionary<string, object?>> messages,
        Dictionary<string, Dictionary<string, object?>> messagesById,
        string toolCallId,
        string? parentMessageId
    )
    {
        if (!string.IsNullOrWhiteSpace(parentMessageId))
        {
            if (messagesById.TryGetValue(parentMessageId, out var parentMessage))
            {
                if (parentMessage.GetValueOrDefault("role") as string == "assistant")
                    return parentMessage;

                return EnsureAssistantMessage(messages, messagesById, toolCallId);
            }

            return EnsureAssistantMessage(messages, messagesById, parentMessageId);
        }

        return EnsureAssistantMessage(messages, messagesById, toolCallId);
    }

    private static Dictionary<string, object?> EnsureAssistantMessage(List<Dictionary<string, object?>> messages, Dictionary<string, Dictionary<string, object?>> messagesById, string messageId)
    {
        if (messagesById.TryGetValue(messageId, out var existing))
            return existing;

        var message = new Dictionary<string, object?>()
        {
            ["id"] = messageId,
            ["role"] = "assistant",
            ["toolCalls"] = new List<Dictionary<string, object?>>(),
        };

        messages.Add(message);
        messagesById[messageId] = message;
        return message;
    }

    private static List<Dictionary<string, object?>> GetOrCreateToolCalls(Dictionary<string, object?> assistantMessage)
    {
        if (assistantMessage.GetValueOrDefault("toolCalls") is List<Dictionary<string, object?>> toolCalls)
            return toolCalls;

        toolCalls = [];
        assistantMessage["toolCalls"] = toolCalls;
        return toolCalls;
    }

    private static void ApplyPatch(JsonNode target, JsonElement patch)
    {
        foreach (var operation in patch.EnumerateArray())
        {
            var op = ReadRequiredString(operation, "op");
            var path = ReadRequiredString(operation, "path");

            switch (op)
            {
                case "add":
                case "replace":
                    if (!operation.TryGetProperty("value", out var value))
                        continue;

                    SetPathValue(target, path, JsonNode.Parse(value.GetRawText()), replace: op == "replace");
                    break;
                case "remove":
                    RemovePathValue(target, path);
                    break;
            }
        }
    }

    private static void SetPathValue(JsonNode target, string path, JsonNode? value, bool replace)
    {
        var segments = ParseJsonPointer(path);
        if (segments.Count == 0)
            return;

        var parent = ResolveParent(target, segments);
        var finalSegment = segments[^1];

        switch (parent)
        {
            case JsonObject jsonObject:
                if (!replace || jsonObject.ContainsKey(finalSegment))
                    jsonObject[finalSegment] = value;
                break;
            case JsonArray jsonArray:
                if (finalSegment == "-")
                {
                    if (!replace)
                        jsonArray.Add(value);
                    break;
                }

                if (!int.TryParse(finalSegment, out var index))
                    break;

                if (replace && index >= 0 && index < jsonArray.Count)
                    jsonArray[index] = value;
                else if (!replace && index >= 0 && index <= jsonArray.Count)
                    jsonArray.Insert(index, value);
                break;
        }
    }

    private static void RemovePathValue(JsonNode target, string path)
    {
        var segments = ParseJsonPointer(path);
        if (segments.Count == 0)
            return;

        var parent = ResolveParent(target, segments);
        var finalSegment = segments[^1];

        switch (parent)
        {
            case JsonObject jsonObject:
                jsonObject.Remove(finalSegment);
                break;
            case JsonArray jsonArray when int.TryParse(finalSegment, out var index) && index >= 0 && index < jsonArray.Count:
                jsonArray.RemoveAt(index);
                break;
        }
    }

    private static JsonNode? ResolveParent(JsonNode target, IReadOnlyList<string> segments)
    {
        JsonNode? current = target;
        for (var index = 0; index < segments.Count - 1; index += 1)
        {
            current = current switch
            {
                JsonObject jsonObject => jsonObject[segments[index]],
                JsonArray jsonArray when int.TryParse(segments[index], out var arrayIndex) && arrayIndex >= 0 && arrayIndex < jsonArray.Count => jsonArray[arrayIndex],
                _ => null,
            };

            if (current is null)
                return null;
        }

        return current;
    }

    private static List<string> ParseJsonPointer(string path)
    {
        if (string.IsNullOrEmpty(path))
            return [];

        return path
            .Split('/', StringSplitOptions.None)
            .Skip(1)
            .Select(segment => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))
            .ToList();
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string? GetToolCallId(StreamEvent streamEvent)
    {
        return string.IsNullOrWhiteSpace(streamEvent.ToolCallId) ? streamEvent.MessageId : streamEvent.ToolCallId;
    }

    private static string GetProtocolReasoningMessageId(string messageId)
    {
        return $"reason:{messageId.Trim()}";
    }

    private static string GetProtocolToolMessageId(string messageId)
    {
        return $"tool:{messageId.Trim()}";
    }

    private static string GetProtocolToolMessageId(string messageId, string toolCallId)
    {
        return $"tool:{messageId.Trim()}:{toolCallId.Trim()}";
    }

    private static string GetProtocolActivityMessageId(string messageId)
    {
        return $"activity:{messageId.Trim()}";
    }

    private static string MapTextRole(StreamMessageRole? role)
    {
        return role switch
        {
            StreamMessageRole.User => "user",
            StreamMessageRole.Developer => "developer",
            StreamMessageRole.System => "system",
            _ => "assistant",
        };
    }

    private enum HistoryCandidateKind
    {
        Text,
        Reasoning,
        Activity,
        AssistantToolCall,
        ToolResult,
    }

    private sealed record HistoryCandidate(
        HistoryCandidateKind Kind,
        string? MessageId,
        string? ProtocolMessageId,
        string? ToolCallId,
        int LatestSequenceNumber
    );
}
