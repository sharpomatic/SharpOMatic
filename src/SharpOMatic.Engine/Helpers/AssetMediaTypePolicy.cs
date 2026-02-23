namespace SharpOMatic.Engine.Helpers;

public static class AssetMediaTypePolicy
{
    private static readonly HashSet<string> AdditionalTextLikeTypes = new(StringComparer.OrdinalIgnoreCase) { "application/json", "application/xml" };

    public static string Normalize(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
            return string.Empty;

        return mediaType.Split(';', 2)[0].Trim();
    }

    public static bool IsTextLike(string? mediaType)
    {
        var normalized = Normalize(mediaType);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (normalized.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;

        return AdditionalTextLikeTypes.Contains(normalized);
    }
}
