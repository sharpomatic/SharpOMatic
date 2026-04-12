namespace SharpOMatic.Engine.Helpers;

public static class AssetStorageKey
{
    private const string LibraryPrefix = "library";
    private const string LibraryFolderPrefix = "folders";
    private const string RunPrefix = "runs";
    private const string ConversationPrefix = "conversations";

    public static string ForLibrary(Guid assetId, Guid? folderId = null)
    {
        if (folderId.HasValue)
            return $"{LibraryPrefix}/{LibraryFolderPrefix}/{folderId.Value:N}/{assetId:N}";

        return $"{LibraryPrefix}/{assetId:N}";
    }

    public static string ForRun(Guid runId, Guid assetId) => $"{RunPrefix}/{runId:N}/{assetId:N}";

    public static string ForConversation(string conversationId, Guid assetId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new SharpOMaticException("Conversation assets require a conversationId.");

        var normalizedConversationId = conversationId.Trim();
        return $"{ConversationPrefix}/{Uri.EscapeDataString(normalizedConversationId)}/{assetId:N}";
    }

    public static string ForScope(AssetScope scope, Guid assetId, Guid? runId = null, string? conversationId = null, Guid? folderId = null)
    {
        return scope switch
        {
            AssetScope.Library => ForLibrary(assetId, folderId),
            AssetScope.Run when runId.HasValue => ForRun(runId.Value, assetId),
            AssetScope.Run => throw new SharpOMaticException("Run assets require a runId."),
            AssetScope.Conversation when !string.IsNullOrWhiteSpace(conversationId) => ForConversation(conversationId, assetId),
            AssetScope.Conversation => throw new SharpOMaticException("Conversation assets require a conversationId."),
            _ => throw new SharpOMaticException($"Unsupported asset scope '{scope}'."),
        };
    }

    public static IReadOnlyList<string> GetSegments(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new SharpOMaticException("Storage key is required.");

        if (storageKey.StartsWith("/", StringComparison.Ordinal) || storageKey.StartsWith("\\", StringComparison.Ordinal))
        {
            throw new SharpOMaticException("Storage key must be relative.");
        }

        if (storageKey.Contains("..", StringComparison.Ordinal))
            throw new SharpOMaticException("Storage key cannot contain '..'.");

        if (storageKey.Contains('\\'))
            throw new SharpOMaticException("Storage key cannot contain backslashes.");

        var segments = storageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            throw new SharpOMaticException("Storage key is invalid.");

        if (segments.Any(segment => segment is "." or ".." || segment.Contains(':')))
            throw new SharpOMaticException("Storage key contains invalid segments.");

        return segments;
    }
}
