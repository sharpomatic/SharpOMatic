namespace SharpOMatic.Engine.Helpers;

internal static class ChatHistoryReplayHelper
{
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
