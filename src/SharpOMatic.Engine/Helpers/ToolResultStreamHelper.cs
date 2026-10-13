namespace SharpOMatic.Engine.Helpers;

/// <summary>
/// Serializes a tool result for the <c>ToolCallResult</c> stream event, as opposed to the result the model is given.
/// </summary>
/// <remarks>
/// A tool can return binary content, such as an image as a <see cref="DataContent"/>, for the model to look at. The model
/// receives it unchanged, but the stream event carries only a note in its place: the event is persisted with the run and
/// replayed to AG-UI clients, and a base64 payload of hundreds of kilobytes per call is storage and bandwidth that no
/// client renders. The call, its result event and every other part of the result are kept, so a client still sees that
/// the tool ran and what it said.
/// </remarks>
public static class ToolResultStreamHelper
{
    private const string TypeDiscriminator = "$type";
    private const string DataTypeName = "data";
    private const string Base64Marker = ";base64,";

    public static string Serialize(FunctionResultContent functionResultContent)
    {
        if (functionResultContent.Result is null)
            return string.Empty;

        if (functionResultContent.Result is string resultText)
            return resultText;

        string json;
        try
        {
            json = JsonSerializer.Serialize(functionResultContent.Result);
        }
        catch
        {
            return functionResultContent.Result.ToString() ?? string.Empty;
        }

        return OmitBinaryContent(json);
    }

    /// <summary>
    /// Replaces every serialized <see cref="DataContent"/> in a JSON tool result with a text note naming its media type
    /// and size. Text that is not JSON, or holds no binary content, is returned unchanged.
    /// </summary>
    /// <remarks>
    /// Works on the JSON rather than the result object because a tool's return value reaches the stream in several
    /// shapes - an <see cref="AIContent"/> array, an <c>object[]</c> clone of one, or a <see cref="JsonElement"/> already
    /// marshalled by the function invoker - and all of them serialize binary parts the same way.
    /// </remarks>
    public static string OmitBinaryContent(string json)
    {
        if (string.IsNullOrEmpty(json) || !json.Contains(Base64Marker, StringComparison.OrdinalIgnoreCase))
            return json;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return json;
        }

        if (root is null)
            return json;

        if (IsDataPart(root))
            return CreateNote(root).ToJsonString();

        return ReplaceDataParts(root) ? root.ToJsonString() : json;
    }

    private static bool ReplaceDataParts(JsonNode node)
    {
        var replaced = false;

        switch (node)
        {
            case JsonArray array:
                for (var i = 0; i < array.Count; i += 1)
                {
                    if (array[i] is not { } item)
                        continue;

                    if (IsDataPart(item))
                    {
                        array[i] = CreateNote(item);
                        replaced = true;
                    }
                    else
                    {
                        replaced |= ReplaceDataParts(item);
                    }
                }

                break;

            case JsonObject obj:
                foreach (var name in obj.Select(property => property.Key).ToList())
                {
                    if (obj[name] is not { } value)
                        continue;

                    if (IsDataPart(value))
                    {
                        obj[name] = CreateNote(value);
                        replaced = true;
                    }
                    else
                    {
                        replaced |= ReplaceDataParts(value);
                    }
                }

                break;
        }

        return replaced;
    }

    /// <summary>
    /// A serialized <see cref="DataContent"/>: one carrying the <c>data</c> discriminator, or any object whose
    /// <c>Uri</c> is a <c>data:</c> URI.
    /// </summary>
    /// <remarks>
    /// The discriminator is only written when a value is serialized through its <see cref="AIContent"/> base type, as
    /// array elements are. A <see cref="DataContent"/> held in an <c>object</c> or a typed property serializes without
    /// it, so the inline data is what identifies it.
    /// </remarks>
    private static bool IsDataPart(JsonNode node)
    {
        if (node is not JsonObject obj)
            return false;

        if (GetString(obj, TypeDiscriminator) is { } typeName && string.Equals(typeName, DataTypeName, StringComparison.Ordinal))
            return true;

        return GetString(obj, "Uri") is { } uri && uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject CreateNote(JsonNode dataPart)
    {
        var (mediaType, bytes) = DescribeDataPart((JsonObject)dataPart);
        var size = bytes is { } length ? $", {Math.Max(1, length / 1024):N0} KB" : string.Empty;

        return new JsonObject { [TypeDiscriminator] = "text", ["Text"] = $"[{mediaType}{size}, omitted from the stream event.]" };
    }

    private static (string MediaType, long? Bytes) DescribeDataPart(JsonObject dataPart)
    {
        var uri = GetString(dataPart, "Uri");
        var mediaType = GetString(dataPart, "MediaType");

        if (uri is null || !uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return (mediaType ?? "binary data", null);

        var comma = uri.IndexOf(',');
        if (comma < 0)
            return (mediaType ?? "binary data", null);

        var header = uri[5..comma];
        var isBase64 = header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        mediaType ??= isBase64 ? header[..^";base64".Length] : header;

        var payloadLength = uri.Length - comma - 1;
        long bytes = isBase64 ? payloadLength * 3L / 4 : payloadLength;

        return (string.IsNullOrEmpty(mediaType) ? "binary data" : mediaType, bytes);
    }

    // Case-insensitive because the casing depends on the serializer options that marshalled the result:
    // PascalCase by default, camelCase under AIJsonUtilities' web defaults.
    private static string? GetString(JsonObject obj, string name)
    {
        foreach (var property in obj)
        {
            if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase) && property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                return text;
        }

        return null;
    }
}
