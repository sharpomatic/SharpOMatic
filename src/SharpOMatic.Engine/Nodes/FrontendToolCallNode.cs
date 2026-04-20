namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.FrontendToolCall)]
public sealed class FrontendToolCallNode(ThreadContext threadContext, FrontendToolCallNodeEntity node) : RunNode<FrontendToolCallNodeEntity>(threadContext, node)
{
    private const string PendingRootPath = "_frontendToolCall";
    private const string ToolResultOutputName = "toolResult";
    private const string OtherInputOutputName = "otherInput";
    private const string ChatContextPath = "input.chat";

    protected override async Task<NodeExecutionResult> RunInternal()
    {
        EnsureConversation();

        var toolCallId = Guid.NewGuid().ToString("D");
        var argumentsJson = await ResolveArgumentsJson();
        var assistantMessageId = $"frontend-tool-call:{toolCallId}";

        var createdEvents = await AppendStreamEvents(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = Node.FunctionName,
                ParentMessageId = assistantMessageId,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = argumentsJson,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
            }
        );

        var pendingState = new ContextObject
        {
            ["toolCallId"] = toolCallId,
            ["functionName"] = Node.FunctionName,
            ["argumentsJson"] = argumentsJson,
            ["streamEventIds"] = ToStringList(createdEvents.Select(e => e.StreamEventId.ToString("D"))),
            ["chatMessageIds"] = new ContextList(),
        };

        ThreadContext.NodeContext[PendingRootPath] = pendingState;

        if (Node.ChatPersistenceMode != FrontendToolCallChatPersistenceMode.None)
        {
            var arguments = ParseArgumentsObject(argumentsJson, $"Frontend Tool Call node '{Node.Title}' arguments must decode to a JSON object when chat persistence is enabled.");
            var functionCallMessage = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(toolCallId, Node.FunctionName, arguments)])
            {
                MessageId = assistantMessageId,
            };

            GetOrCreateChatHistory().Add(functionCallMessage);
            pendingState.Set("chatMessageIds", ToStringList([assistantMessageId!]));
        }

        return NodeExecutionResult.Suspend($"Waiting for frontend tool call '{Node.FunctionName}'.");
    }

    protected override async Task<NodeExecutionResult> ResumeInternal(NodeResumeInput input)
    {
        EnsureConversation();

        if (input is not ContextMergeResumeInput contextMerge)
            throw new SharpOMaticException($"Frontend Tool Call node cannot handle resume input type '{input.GetType().Name}'.");

        ContextHelpers.OverwriteContexts(ThreadContext.NodeContext, contextMerge.Context);

        var pendingState = GetPendingState();
        try
        {
            var incomingMessage = ReadSingleIncomingMessage();
            var expectedToolCallId = ReadRequiredPendingString(pendingState, "toolCallId");

            if (incomingMessage.IsMatchingToolResult(expectedToolCallId))
            {
                var createdResultEvents = await AppendStreamEvents(
                    new StreamEventWrite()
                    {
                        EventKind = StreamEventKind.ToolCallResult,
                        MessageId = incomingMessage.MessageId,
                        ToolCallId = expectedToolCallId,
                        TextDelta = incomingMessage.Content ?? string.Empty,
                    }
                );

                WriteResultOutput(incomingMessage.Content ?? string.Empty);
                ApplyMatchedChatPersistence(pendingState, incomingMessage.MessageId);

                if (Node.HideFromReplyAfterHandled)
                    await HideStoredStreamEvents(GetStoredStreamEventIds(pendingState).Concat(createdResultEvents.Select(e => e.StreamEventId)));

                return NodeExecutionResult.Continue("Frontend tool result received.", ResolveNamedOutput(ToolResultOutputName));
            }

            RemoveCreatedChatMessages(pendingState);
            await HideStoredStreamEvents(GetStoredStreamEventIds(pendingState));
            return NodeExecutionResult.Continue("Frontend tool call abandoned.", ResolveNamedOutput(OtherInputOutputName));
        }
        finally
        {
            CleanupPendingState();
        }
    }

    private void EnsureConversation()
    {
        if (ProcessContext.Conversation is null)
            throw new SharpOMaticException("Node suspension requires an active conversation.");
    }

    private async Task<string> ResolveArgumentsJson()
    {
        return Node.ArgumentsMode switch
        {
            FrontendToolCallArgumentsMode.ContextPath => await ResolveArgumentsFromContextPath(),
            FrontendToolCallArgumentsMode.FixedJson => ValidateFixedArgumentsJson(Node.ArgumentsJson),
            _ => throw new SharpOMaticException($"Unsupported Frontend Tool Call arguments mode '{Node.ArgumentsMode}'."),
        };
    }

    private async Task<string> ResolveArgumentsFromContextPath()
    {
        if (string.IsNullOrWhiteSpace(Node.ArgumentsPath))
            throw new SharpOMaticException("Frontend Tool Call node requires ArgumentsPath when ArgumentsMode is ContextPath.");

        if (!ThreadContext.NodeContext.TryGet<object?>(Node.ArgumentsPath, out var value))
            throw new SharpOMaticException($"Frontend Tool Call arguments path '{Node.ArgumentsPath}' could not be found.");

        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch (Exception)
        {
            throw new SharpOMaticException($"Frontend Tool Call arguments path '{Node.ArgumentsPath}' could not be serialized to JSON.");
        }
    }

    private static string ValidateFixedArgumentsJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            throw new SharpOMaticException("Frontend Tool Call node requires ArgumentsJson when ArgumentsMode is FixedJson.");

        try
        {
            using var _ = JsonDocument.Parse(rawJson);
            return rawJson;
        }
        catch (JsonException)
        {
            throw new SharpOMaticException("Frontend Tool Call fixed arguments must contain valid JSON.");
        }
    }

    private ContextObject GetPendingState()
    {
        if (!ThreadContext.NodeContext.TryGetObject(PendingRootPath, out var pendingState) || pendingState is null)
            throw new SharpOMaticException($"Frontend Tool Call node '{Node.Title}' does not have pending resume state.");

        return pendingState;
    }

    private IncomingMessage ReadSingleIncomingMessage()
    {
        if (!ThreadContext.NodeContext.TryGetObject("agent", out var agent) || agent is null)
            throw new SharpOMaticException("Frontend Tool Call resume requires AG-UI agent context.");

        if (!agent.TryGetList("messages", out var messages) || messages is null || messages.Count == 0)
            throw new SharpOMaticException("Frontend Tool Call requires exactly one incoming message on resume but received 0.");

        if (messages.Count != 1)
            throw new SharpOMaticException($"Frontend Tool Call requires exactly one incoming message on resume but received {messages.Count}.");

        if (messages[0] is not ContextObject message)
            throw new SharpOMaticException("Frontend Tool Call resume message must be a JSON object.");

        return IncomingMessage.FromContext(message);
    }

    private void WriteResultOutput(string content)
    {
        object? result = content;
        try
        {
            result = ContextHelpers.FastDeserializeString(content);
        }
        catch
        {
            result = content;
        }

        if (!ThreadContext.NodeContext.TrySet(Node.ResultOutputPath, result))
            throw new SharpOMaticException($"Could not set '{Node.ResultOutputPath}' into context.");
    }

    private void ApplyMatchedChatPersistence(ContextObject pendingState, string toolResultMessageId)
    {
        switch (Node.ChatPersistenceMode)
        {
            case FrontendToolCallChatPersistenceMode.None:
                RemoveCreatedChatMessages(pendingState);
                RemoveChatMessagesByIds([toolResultMessageId]);
                break;
            case FrontendToolCallChatPersistenceMode.FunctionCallOnly:
                RemoveChatMessagesByIds([toolResultMessageId]);
                break;
            case FrontendToolCallChatPersistenceMode.FunctionCallAndResult:
                break;
            default:
                throw new SharpOMaticException($"Unsupported Frontend Tool Call chat persistence mode '{Node.ChatPersistenceMode}'.");
        }
    }

    private async Task HideStoredStreamEvents(IEnumerable<Guid> streamEventIds)
    {
        var ids = streamEventIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return;

        await ProcessContext.RepositoryService.UpdateStreamEventsHideFromReply(ids, hideFromReply: true);
    }

    private void CleanupPendingState()
    {
        ThreadContext.NodeContext.Remove(PendingRootPath);
    }

    private void RemoveCreatedChatMessages(ContextObject pendingState)
    {
        RemoveChatMessagesByIds(ReadPendingStringList(pendingState, "chatMessageIds"));
    }

    private void RemoveChatMessagesByIds(IEnumerable<string> messageIds)
    {
        var ids = messageIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0)
            return;

        if (!ThreadContext.NodeContext.TryGet<ContextList>(ChatContextPath, out var chatHistory) || chatHistory is null)
            return;

        for (var i = chatHistory.Count - 1; i >= 0; i -= 1)
        {
            if (chatHistory[i] is ChatMessage message && !string.IsNullOrWhiteSpace(message.MessageId) && ids.Contains(message.MessageId))
                chatHistory.RemoveAt(i);
        }
    }

    private ContextList GetOrCreateChatHistory()
    {
        if (ThreadContext.NodeContext.TryGet<ContextList>(ChatContextPath, out var chatHistory) && chatHistory is not null)
            return chatHistory;

        chatHistory = [];
        ThreadContext.NodeContext.Set(ChatContextPath, chatHistory);
        return chatHistory;
    }

    private List<NextNodeData> ResolveNamedOutput(string outputName)
    {
        var connector = Node.Outputs.FirstOrDefault(output => string.Equals(output.Name, outputName, StringComparison.Ordinal));
        if (connector is null)
            throw new SharpOMaticException($"Frontend Tool Call node is missing required '{outputName}' output.");

        if (!IsOutputConnected(connector))
            return [];

        return [new NextNodeData(ThreadContext, WorkflowContext.ResolveOutput(connector))];
    }

    private static IDictionary<string, object?> ParseArgumentsObject(string argumentsJson, string errorMessage)
    {
        try
        {
            var parsed = ContextHelpers.FastDeserializeString(argumentsJson);
            if (parsed is not ContextObject contextObject)
                throw new SharpOMaticException(errorMessage);

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
            throw new SharpOMaticException(errorMessage);
        }
    }

    private static string ReadRequiredPendingString(ContextObject pendingState, string propertyName)
    {
        if (pendingState.TryGet<string>(propertyName, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;

        throw new SharpOMaticException($"Frontend Tool Call pending state is missing '{propertyName}'.");
    }

    private static IReadOnlyList<string> ReadPendingStringList(ContextObject pendingState, string propertyName)
    {
        if (!pendingState.TryGetList(propertyName, out var list) || list is null)
            return [];

        return list
            .OfType<string>()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static IReadOnlyList<Guid> GetStoredStreamEventIds(ContextObject pendingState)
    {
        return ReadPendingStringList(pendingState, "streamEventIds")
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    private static ContextList ToStringList(IEnumerable<string> values)
    {
        ContextList list = [];
        foreach (var value in values)
            list.Add(value);

        return list;
    }

    private sealed record IncomingMessage(string MessageId, string? Role, string? ToolCallId, string? Content)
    {
        public bool IsMatchingToolResult(string expectedToolCallId)
        {
            return string.Equals(Role, "tool", StringComparison.OrdinalIgnoreCase)
                && string.Equals(ToolCallId, expectedToolCallId, StringComparison.Ordinal);
        }

        public static IncomingMessage FromContext(ContextObject message)
        {
            if (!message.TryGet<string>("id", out var messageId) || string.IsNullOrWhiteSpace(messageId))
                throw new SharpOMaticException("Frontend Tool Call resume message must include a non-empty id.");

            message.TryGet<string>("role", out var role);
            message.TryGet<string>("toolCallId", out var toolCallId);

            string? content = null;
            if (message.TryGet<object?>("content", out var rawContent) && rawContent is not null)
            {
                if (rawContent is not string contentText)
                    throw new SharpOMaticException("Frontend Tool Call resume message content must be a string.");

                content = contentText;
            }

            return new IncomingMessage(messageId, role, toolCallId, content);
        }
    }
}
