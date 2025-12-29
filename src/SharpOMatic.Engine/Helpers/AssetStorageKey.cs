namespace SharpOMatic.Engine.Helpers;

public static class AssetStorageKey
{
    private const string LibraryPrefix = "library";
    private const string RunPrefix = "runs";

    public static string ForLibrary(Guid assetId)
        => $"{LibraryPrefix}/{assetId:N}";

    public static string ForRun(Guid runId, Guid assetId)
        => $"{RunPrefix}/{runId:N}/{assetId:N}";

    public static string ForScope(AssetScope scope, Guid assetId, Guid? runId = null)
    {
        return scope switch
        {
            AssetScope.Library => ForLibrary(assetId),
            AssetScope.Run when runId.HasValue => ForRun(runId.Value, assetId),
            AssetScope.Run => throw new SharpOMaticException("Run assets require a runId."),
            _ => throw new SharpOMaticException($"Unsupported asset scope '{scope}'."),
        };
    }

    public static IReadOnlyList<string> GetSegments(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new SharpOMaticException("Storage key is required.");

        if (storageKey.StartsWith("/", StringComparison.Ordinal) ||
            storageKey.StartsWith("\\", StringComparison.Ordinal))
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
