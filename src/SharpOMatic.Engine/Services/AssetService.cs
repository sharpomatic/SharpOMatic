namespace SharpOMatic.Engine.Services;

public class AssetService(IRepositoryService repositoryService, IAssetStore assetStore) : IAssetService
{
    public async Task<AssetRef> CreateFromStreamAsync(
        Stream content,
        long sizeBytes,
        string name,
        string mediaType,
        AssetScope scope,
        Guid? runId = null)
    {
        if (content is null)
            throw new SharpOMaticException("Asset content is required.");

        if (sizeBytes < 0)
            throw new SharpOMaticException("Asset size cannot be negative.");

        if (string.IsNullOrWhiteSpace(name))
            throw new SharpOMaticException("Asset name is required.");

        if (string.IsNullOrWhiteSpace(mediaType))
            throw new SharpOMaticException("Asset media type is required.");

        var assetId = Guid.NewGuid();
        var storageKey = AssetStorageKey.ForScope(scope, assetId, runId);

        await assetStore.SaveAsync(storageKey, content);

        var asset = new Asset
        {
            AssetId = assetId,
            RunId = scope == AssetScope.Run ? runId : null,
            Name = name.Trim(),
            Scope = scope,
            Created = DateTime.Now,
            MediaType = mediaType.Trim(),
            SizeBytes = sizeBytes,
            StorageKey = storageKey
        };

        await repositoryService.UpsertAsset(asset);

        return new AssetRef
        {
            AssetId = asset.AssetId,
            Name = asset.Name,
            MediaType = asset.MediaType,
            SizeBytes = asset.SizeBytes
        };
    }

    public async Task<AssetRef> CreateFromBytesAsync(
        byte[] data,
        string name,
        string mediaType,
        AssetScope scope,
        Guid? runId = null)
    {
        if (data is null)
            throw new SharpOMaticException("Asset data is required.");

        await using var stream = new MemoryStream(data, writable: false);
        return await CreateFromStreamAsync(stream, data.LongLength, name, mediaType, scope, runId);
    }
}
