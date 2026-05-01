namespace SharpOMatic.Engine.Helpers;

internal static class ChatHistoryReplayHelper
{
    public static ContextList CreatePortableOutputMessages(IEnumerable<ChatMessage> messages, bool dropToolCalls)
    {
        ContextList portableMessages = [];
        Dictionary<string, FunctionCallContent> toolCallsById = new(StringComparer.Ordinal);
        Queue<FunctionCallContent> anonymousToolCalls = [];

        foreach (var message in messages)
        {
            List<AIContent> portableContents = [];
            List<ChatMessage> toolResultMessages = [];

            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextReasoningContent:
                        break;

                    case TextContent textContent when IsPortableOutputRole(message.Role) && !string.IsNullOrWhiteSpace(textContent.Text):
                        portableContents.Add(new TextContent(textContent.Text));
                        break;

                    case DataContent dataContent when IsPortableOutputRole(message.Role):
                        portableContents.Add(CloneDataContent(dataContent));
                        break;

                    case UriContent uriContent when IsPortableOutputRole(message.Role):
                        portableContents.Add(CloneUriContent(uriContent));
                        break;

                    case FunctionCallContent functionCallContent when !dropToolCalls:
                        TrackToolCall(functionCallContent, toolCallsById, anonymousToolCalls);
                        break;

                    case FunctionResultContent functionResultContent when !dropToolCalls:
                        toolResultMessages.Add(CreateToolResultAssistantMessage(functionResultContent, toolCallsById, anonymousToolCalls));
                        break;
                }
            }

            if (portableContents.Count > 0)
            {
                portableMessages.Add(
                    new ChatMessage(message.Role, portableContents)
                    {
                        AuthorName = message.AuthorName,
                        CreatedAt = message.CreatedAt,
                        MessageId = message.MessageId,
                    }
                );
            }

            foreach (var toolResultMessage in toolResultMessages)
                portableMessages.Add(toolResultMessage);
        }

        return portableMessages;
    }

    public static void AddPreparedInputMessages(List<ChatMessage> chat, ContextObject nodeContext, string chatInputPath)
    {
        var storedMessages = ReadStoredMessages(nodeContext, chatInputPath);
        if (storedMessages.Count == 0)
            return;

        chat.AddRange(CreatePortableReplayMessages(storedMessages));
    }

    private static List<ChatMessage> ReadStoredMessages(ContextObject nodeContext, string chatInputPath)
    {
        List<ChatMessage> messages = [];
        if (nodeContext.TryGet<ChatMessage>(chatInputPath, out var chatMessage) && (chatMessage is not null))
            messages.Add(chatMessage);
        else if (nodeContext.TryGet<ContextList>(chatInputPath, out var chatList) && (chatList is not null))
        {
            foreach (var listEntry in chatList)
                if (listEntry is ChatMessage message)
                    messages.Add(message);
        }

        return messages;
    }

    private static List<ChatMessage> CreatePortableReplayMessages(IEnumerable<ChatMessage> messages)
    {
        List<ChatMessage> portableMessages = [];

        foreach (var message in messages)
        {
            List<AIContent> portableContents = [];

            foreach (var content in message.Contents)
            {
                var portableContent = ClonePortableContent(content);
                if (portableContent is not null)
                    portableContents.Add(portableContent);
            }

            if (portableContents.Count == 0)
                continue;

            var portableMessage = new ChatMessage(message.Role, portableContents)
            {
                AuthorName = message.AuthorName,
                CreatedAt = message.CreatedAt,
                MessageId = message.MessageId,
            };

            portableMessages.Add(portableMessage);
        }

        return portableMessages;
    }

    private static bool IsPortableOutputRole(ChatRole role)
    {
        return role == ChatRole.User || role == ChatRole.Assistant;
    }

    private static void TrackToolCall(
        FunctionCallContent functionCallContent,
        Dictionary<string, FunctionCallContent> toolCallsById,
        Queue<FunctionCallContent> anonymousToolCalls
    )
    {
        if (!string.IsNullOrWhiteSpace(functionCallContent.CallId))
        {
            toolCallsById[functionCallContent.CallId.Trim()] = functionCallContent;
            return;
        }

        anonymousToolCalls.Enqueue(functionCallContent);
    }

    private static ChatMessage CreateToolResultAssistantMessage(
        FunctionResultContent functionResultContent,
        Dictionary<string, FunctionCallContent> toolCallsById,
        Queue<FunctionCallContent> anonymousToolCalls
    )
    {
        var functionCallContent = ResolveToolCall(functionResultContent, toolCallsById, anonymousToolCalls);
        var toolName = ResolveToolName(functionResultContent, functionCallContent);
        var resultText = SerializeToolResult(functionResultContent.Result);
        var argumentsText = SerializeToolArguments(functionCallContent);
        var text = string.IsNullOrWhiteSpace(argumentsText)
            ? $"Result of calling tool {toolName} with no arguments = {resultText}"
            : $"Result of calling tool {toolName} with arguments {argumentsText} = {resultText}";

        return new ChatMessage(ChatRole.Assistant, [new TextContent(text)]);
    }

    private static FunctionCallContent? ResolveToolCall(
        FunctionResultContent functionResultContent,
        Dictionary<string, FunctionCallContent> toolCallsById,
        Queue<FunctionCallContent> anonymousToolCalls
    )
    {
        if (!string.IsNullOrWhiteSpace(functionResultContent.CallId) && toolCallsById.Remove(functionResultContent.CallId.Trim(), out var functionCallContent))
            return functionCallContent;

        return anonymousToolCalls.Count > 0
            ? anonymousToolCalls.Dequeue()
            : null;
    }

    private static string ResolveToolName(FunctionResultContent functionResultContent, FunctionCallContent? functionCallContent)
    {
        if (!string.IsNullOrWhiteSpace(functionCallContent?.Name))
            return functionCallContent.Name.Trim();

        if (!string.IsNullOrWhiteSpace(functionResultContent.CallId))
            return functionResultContent.CallId.Trim();

        return "unknown";
    }

    private static string? SerializeToolArguments(FunctionCallContent? functionCallContent)
    {
        if ((functionCallContent?.Arguments is null) || (functionCallContent.Arguments.Count == 0))
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

    private static string SerializeToolResult(object? result)
    {
        if (result is null)
            return string.Empty;

        if (result is string resultText)
            return resultText;

        try
        {
            return JsonSerializer.Serialize(result);
        }
        catch
        {
            return result.ToString() ?? string.Empty;
        }
    }

    private static AIContent? ClonePortableContent(AIContent content)
    {
        return content switch
        {
            TextReasoningContent => null,
            TextContent textContent when !string.IsNullOrWhiteSpace(textContent.Text) => new TextContent(textContent.Text),
            FunctionCallContent functionCallContent => new FunctionCallContent(functionCallContent.CallId, functionCallContent.Name, CloneDictionary(functionCallContent.Arguments)),
            FunctionResultContent functionResultContent => new FunctionResultContent(functionResultContent.CallId, CloneValue(functionResultContent.Result)),
            DataContent dataContent => CloneDataContent(dataContent),
            UriContent uriContent => CloneUriContent(uriContent),
            _ => null,
        };
    }

    private static DataContent CloneDataContent(DataContent dataContent)
    {
        var clone = new DataContent(dataContent.Data, dataContent.MediaType)
        {
            Name = dataContent.Name,
        };
        return clone;
    }

    private static UriContent CloneUriContent(UriContent uriContent)
    {
        return uriContent.Uri is not null
            ? new UriContent(uriContent.Uri, uriContent.MediaType)
            : new UriContent(string.Empty, uriContent.MediaType);
    }

    private static IDictionary<string, object?> CloneDictionary(IDictionary<string, object?>? source)
    {
        Dictionary<string, object?> clone = [];
        if (source is null)
            return clone;

        foreach (var entry in source)
            clone[entry.Key] = CloneValue(entry.Value);

        return clone;
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or Guid or DateTime or DateTimeOffset or TimeSpan => value,
            JsonElement jsonElement => jsonElement.Clone(),
            ContextObject contextObject => CloneContextObject(contextObject),
            ContextList contextList => CloneContextList(contextList),
            IDictionary<string, object?> dictionary => CloneDictionary(dictionary),
            IEnumerable enumerable when value is not string => CloneArray(enumerable),
            _ => value,
        };
    }

    private static ContextObject CloneContextObject(ContextObject source)
    {
        var clone = new ContextObject();
        foreach (var entry in source)
            clone[entry.Key] = CloneValue(entry.Value);

        return clone;
    }

    private static ContextList CloneContextList(ContextList source)
    {
        ContextList clone = [];
        foreach (var item in source)
            clone.Add(CloneValue(item));

        return clone;
    }

    private static object?[] CloneArray(IEnumerable source)
    {
        List<object?> clone = [];
        foreach (var item in source)
            clone.Add(CloneValue(item));

        return [.. clone];
    }
}
