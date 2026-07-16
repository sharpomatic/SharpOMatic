namespace SharpOMatic.Engine.Helpers;

public static class AssetNameParser
{
    public static bool IsValidAssetName(string? raw)
    {
        return !string.IsNullOrWhiteSpace(raw) && !raw.Contains(',', StringComparison.Ordinal);
    }

    public static bool IsValidFolderName(string? raw)
    {
        return !string.IsNullOrWhiteSpace(raw) && !raw.Contains(',', StringComparison.Ordinal) && !raw.Contains('/', StringComparison.Ordinal) && !raw.Contains('\\', StringComparison.Ordinal);
    }

    public static bool TryParseFolderQualifiedName(string raw, out string folderName, out string assetName)
    {
        folderName = string.Empty;
        assetName = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        var separator = trimmed.IndexOf('/');
        if (separator <= 0 || separator >= trimmed.Length - 1)
            return false;

        if (trimmed.IndexOf('/', separator + 1) >= 0)
            return false;

        var folder = trimmed[..separator].Trim();
        var name = trimmed[(separator + 1)..].Trim();
        if (!IsValidFolderName(folder) || !IsValidAssetName(name))
            return false;

        folderName = folder;
        assetName = name;
        return true;
    }
}
