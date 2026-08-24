namespace SharpOMatic.Engine.Helpers;

internal static class ChatHistoryReplayHelper
{
    public static ContextList CreatePortableOutputMessages(IEnumerable<ChatMessage> messages, bool dropToolCalls)
    {
        List<ChatMessage> sourceMessages = [.. messages];
        ContextList portableMessages = [];

        // Tool calls are written as native FunctionCallContent/FunctionResultContent, preserving the roles and
        // the per-message grouping the provider produced. Only matched pairs are portable: an unanswered call is
        // rejected by every provider ("An assistant message with 'tool_calls' must be followed by tool messages
        // responding to each 'tool_call_id'"), and a result with no call is invalid for the same reason. The
        // engine creates the unanswered shape itself whenever RemoveModelCallExitToolResults strips an exit
        // sentinel result, so this filter is load-bearing rather than defensive. When DropToolCalls is set the
        // pair set stays empty, which removes all tool content through the same code path.
        HashSet<string> pairedCallIds = dropToolCalls ? [] : FindPairedCallIds(sourceMessages);

        foreach (var message in sourceMessages)
        {
            List<AIContent> portableContents = [];

            foreach (var content in message.Contents)
            {
                var portableContent = ClonePortableOutputContent(content, message.Role, pairedCallIds);
                if (portableContent is not null)
                    portableContents.Add(portableContent);
            }

            if (portableContents.Count == 0)
                continue;

            portableMessages.Add(
                new ChatMessage(message.Role, portableContents)
                {
                    AuthorName = message.AuthorName,
                    CreatedAt = message.CreatedAt,
                    MessageId = message.MessageId,
                }
            );
        }

        return portableMessages;
    }

    public static List<ChatMessage> CreatePortableStoredMessages(IEnumerable<ChatMessage> messages)
    {
        return CreatePortableReplayMessages(messages);
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

    private static HashSet<string> FindPairedCallIds(List<ChatMessage> messages)
    {
        HashSet<string> callIds = new(StringComparer.Ordinal);
        HashSet<string> resultIds = new(StringComparer.Ordinal);

        foreach (var message in messages)
        {
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent functionCallContent when NormalizeCallId(functionCallContent.CallId) is { } callId:
                        callIds.Add(callId);
                        break;

                    case FunctionResultContent functionResultContent when NormalizeCallId(functionResultContent.CallId) is { } resultId:
                        resultIds.Add(resultId);
                        break;
                }
            }
        }

        // A call without an id cannot be matched to its result, so it is never portable and never lands here.
        callIds.IntersectWith(resultIds);
        return callIds;
    }

    private static bool IsPairedCallId(string? callId, HashSet<string> pairedCallIds)
    {
        return (NormalizeCallId(callId) is { } normalizedCallId) && pairedCallIds.Contains(normalizedCallId);
    }

    private static string? NormalizeCallId(string? callId)
    {
        return string.IsNullOrWhiteSpace(callId)
            ? null
            : callId.Trim();
    }

    // The output side drops reasoning content and restricts text/data/uri content to conversational roles,
    // because a stored transcript is replayed to a model that may not be the one that produced it. The input
    // side (ClonePortableContent) is deliberately more permissive: it round-trips whatever a workflow chose to
    // store, including tool content written by the Frontend/Backend Tool Call nodes.
    private static AIContent? ClonePortableOutputContent(AIContent content, ChatRole role, HashSet<string> pairedCallIds)
    {
        return content switch
        {
            TextReasoningContent => null,
            TextContent textContent when IsPortableOutputRole(role) && !string.IsNullOrWhiteSpace(textContent.Text) => new TextContent(textContent.Text),
            DataContent dataContent when IsPortableOutputRole(role) => CloneDataContent(dataContent),
            UriContent uriContent when IsPortableOutputRole(role) => CloneUriContent(uriContent),
            FunctionCallContent functionCallContent when IsPairedCallId(functionCallContent.CallId, pairedCallIds) => new FunctionCallContent(
                functionCallContent.CallId,
                functionCallContent.Name,
                CloneDictionary(functionCallContent.Arguments)
            ),
            FunctionResultContent functionResultContent when IsPairedCallId(functionResultContent.CallId, pairedCallIds) => new FunctionResultContent(
                functionResultContent.CallId,
                CloneValue(functionResultContent.Result)
            ),
            _ => null,
        };
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
