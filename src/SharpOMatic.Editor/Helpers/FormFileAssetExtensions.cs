namespace SharpOMatic.Editor.Helpers;

public static class FormFileAssetExtensions
{
    public static async Task<AssetRef> CreateAssetRefAsync(this IFormFile file, IAssetService assetService, AssetScope scope, Guid? runId = null, string? name = null, string? mediaType = null)
    {
        if (file is null)
            throw new SharpOMaticException("File is required.");

        if (file.Length <= 0)
            throw new SharpOMaticException("File is empty.");

        var resolvedName = string.IsNullOrWhiteSpace(name) ? file.FileName : name;
        if (string.IsNullOrWhiteSpace(resolvedName))
            throw new SharpOMaticException("Asset name is required.");

        var resolvedMediaType = string.IsNullOrWhiteSpace(mediaType) ? (string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType) : mediaType;

        await using var stream = file.OpenReadStream();
        return await assetService.CreateFromStreamAsync(stream, file.Length, resolvedName, resolvedMediaType, scope, runId);
    }
}
