namespace SharpOMatic.Engine.Services;

public class AssetService(IRepositoryService repositoryService, IAssetStore assetStore) : IAssetService
{
    public async Task<AssetRef> CreateFromStreamAsync(Stream content, long sizeBytes, string name, string mediaType, AssetScope scope, Guid? runId = null, Guid? conversationId = null, Guid? folderId = null)
    {
        if (content is null)
            throw new SharpOMaticException("Asset content is required.");

        if (sizeBytes < 0)
            throw new SharpOMaticException("Asset size cannot be negative.");

        if (string.IsNullOrWhiteSpace(name))
            throw new SharpOMaticException("Asset name is required.");

        if (string.IsNullOrWhiteSpace(mediaType))
            throw new SharpOMaticException("Asset media type is required.");

        if (scope == AssetScope.Run && !runId.HasValue)
            throw new SharpOMaticException("Run assets require a runId.");

        if (scope == AssetScope.Run && folderId.HasValue)
            throw new SharpOMaticException("Run assets cannot be assigned to a folder.");

        if (scope == AssetScope.Conversation && !conversationId.HasValue)
            throw new SharpOMaticException("Conversation assets require a conversationId.");

        if (scope == AssetScope.Conversation && folderId.HasValue)
            throw new SharpOMaticException("Conversation assets cannot be assigned to a folder.");

        if (scope == AssetScope.Library && folderId.HasValue)
            _ = await repositoryService.GetAssetFolder(folderId.Value);

        var assetId = Guid.NewGuid();
        var storageKey = AssetStorageKey.ForScope(scope, assetId, runId, conversationId, folderId);

        await assetStore.SaveAsync(storageKey, content);

        var asset = new Asset
        {
            AssetId = assetId,
            RunId = scope == AssetScope.Run ? runId : null,
            ConversationId = scope == AssetScope.Conversation ? conversationId : null,
            FolderId = scope == AssetScope.Library ? folderId : null,
            Name = name.Trim(),
            Scope = scope,
            Created = DateTime.Now,
            MediaType = mediaType.Trim(),
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
        };

        await repositoryService.UpsertAsset(asset);

        return new AssetRef(asset);
    }

    public async Task<AssetRef> CreateFromBytesAsync(byte[] data, string name, string mediaType, AssetScope scope, Guid? runId = null, Guid? conversationId = null, Guid? folderId = null)
    {
        if (data is null)
            throw new SharpOMaticException("Asset data is required.");

        await using var stream = new MemoryStream(data, writable: false);
        return await CreateFromStreamAsync(stream, data.LongLength, name, mediaType, scope, runId, conversationId, folderId);
    }
}
