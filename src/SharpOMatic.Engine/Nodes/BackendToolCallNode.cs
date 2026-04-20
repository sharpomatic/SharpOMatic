namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.BackendToolCall)]
public sealed class BackendToolCallNode(ThreadContext threadContext, BackendToolCallNodeEntity node) : RunNode<BackendToolCallNodeEntity>(threadContext, node)
{
    private const string ChatContextPath = "input.chat";

    protected override async Task<NodeExecutionResult> RunInternal()
    {
        var toolCallId = Guid.NewGuid().ToString("D");
        var resultMessageId = $"backend-tool-result:{toolCallId}";
        var argumentsJson = await ResolveJson(Node.ArgumentsMode, Node.ArgumentsPath, Node.ArgumentsJson, "Arguments");
        var resultContent = await ResolveResultContent();

        await AppendStreamEvents(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = Node.FunctionName,
                ParentMessageId = string.Empty,
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
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallResult,
                MessageId = resultMessageId,
                ToolCallId = toolCallId,
                TextDelta = resultContent,
            }
        );

        ApplyChatPersistence(toolCallId, resultMessageId, argumentsJson, resultContent);
        return NodeExecutionResult.Continue($"Backend tool call '{Node.FunctionName}' emitted.", ResolveOptionalSingleOutput(ThreadContext));
    }

    private Task<string> ResolveJson(ToolCallDataMode mode, string path, string rawJson, string fieldName)
    {
        return mode switch
        {
            ToolCallDataMode.ContextPath => ResolveFromContextPath(path, fieldName),
            ToolCallDataMode.FixedJson => Task.FromResult(ValidateFixedJson(rawJson, fieldName)),
            _ => throw new SharpOMaticException($"Unsupported Backend Tool Call {fieldName.ToLowerInvariant()} mode '{mode}'."),
        };
    }

    private async Task<string> ResolveResultContent()
    {
        if (Node.ResultMode == ToolCallDataMode.FixedJson)
            return ValidateFixedJson(Node.ResultJson, "Result");

        if (Node.ResultMode != ToolCallDataMode.ContextPath)
            throw new SharpOMaticException($"Unsupported Backend Tool Call result mode '{Node.ResultMode}'.");

        if (string.IsNullOrWhiteSpace(Node.ResultPath))
            throw new SharpOMaticException("Backend Tool Call node requires ResultPath when ResultMode is ContextPath.");

        if (!ThreadContext.NodeContext.TryGet<object?>(Node.ResultPath, out var value))
            throw new SharpOMaticException($"Backend Tool Call result path '{Node.ResultPath}' could not be found.");

        if (value is null)
            return string.Empty;

        if (value is string text)
            return text;

        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch (Exception)
        {
            throw new SharpOMaticException($"Backend Tool Call result path '{Node.ResultPath}' could not be serialized to JSON.");
        }
    }

    private async Task<string> ResolveFromContextPath(string path, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new SharpOMaticException($"Backend Tool Call node requires {fieldName}Path when {fieldName}Mode is ContextPath.");

        if (!ThreadContext.NodeContext.TryGet<object?>(path, out var value))
            throw new SharpOMaticException($"Backend Tool Call {fieldName.ToLowerInvariant()} path '{path}' could not be found.");

        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch (Exception)
        {
            throw new SharpOMaticException($"Backend Tool Call {fieldName.ToLowerInvariant()} path '{path}' could not be serialized to JSON.");
        }
    }

    private static string ValidateFixedJson(string rawJson, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            throw new SharpOMaticException($"Backend Tool Call node requires {fieldName}Json when {fieldName}Mode is FixedJson.");

        try
        {
            using var _ = JsonDocument.Parse(rawJson);
            return rawJson;
        }
        catch (JsonException)
        {
            throw new SharpOMaticException($"Backend Tool Call fixed {fieldName.ToLowerInvariant()} must contain valid JSON.");
        }
    }

    private void ApplyChatPersistence(string toolCallId, string resultMessageId, string argumentsJson, string resultContent)
    {
        if (Node.ChatPersistenceMode == ToolCallChatPersistenceMode.None)
            return;

        var arguments = ParseArgumentsObject(argumentsJson);
        var assistantMessageId = $"backend-tool-call:{toolCallId}";
        var assistantMessage = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(toolCallId, Node.FunctionName, arguments)])
        {
            MessageId = assistantMessageId,
        };

        var chatHistory = GetOrCreateChatHistory();
        chatHistory.Add(assistantMessage);

        if (Node.ChatPersistenceMode == ToolCallChatPersistenceMode.FunctionCallOnly)
            return;

        chatHistory.Add(
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent(toolCallId, ParseResultValue(resultContent))])
            {
                MessageId = resultMessageId,
            }
        );
    }

    private ContextList GetOrCreateChatHistory()
    {
        if (ThreadContext.NodeContext.TryGet<ContextList>(ChatContextPath, out var chatHistory) && chatHistory is not null)
            return chatHistory;

        chatHistory = [];
        ThreadContext.NodeContext.Set(ChatContextPath, chatHistory);
        return chatHistory;
    }

    private static IDictionary<string, object?> ParseArgumentsObject(string argumentsJson)
    {
        try
        {
            var parsed = ContextHelpers.FastDeserializeString(argumentsJson);
            if (parsed is not ContextObject contextObject)
                throw new SharpOMaticException("Backend Tool Call arguments must decode to a JSON object when chat persistence is enabled.");

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
            throw new SharpOMaticException("Backend Tool Call arguments must decode to a JSON object when chat persistence is enabled.");
        }
    }

    private static object? ParseResultValue(string resultContent)
    {
        if (string.IsNullOrWhiteSpace(resultContent))
            return resultContent;

        try
        {
            return ContextHelpers.FastDeserializeString(resultContent);
        }
        catch
        {
            return resultContent;
        }
    }
}
