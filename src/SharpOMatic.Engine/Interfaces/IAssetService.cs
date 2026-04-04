namespace SharpOMatic.Engine.Interfaces;

public interface IAssetService
{
    Task<AssetRef> CreateFromStreamAsync(Stream content, long sizeBytes, string name, string mediaType, AssetScope scope, Guid? runId = null, Guid? conversationId = null, Guid? folderId = null);

    Task<AssetRef> CreateFromBytesAsync(byte[] data, string name, string mediaType, AssetScope scope, Guid? runId = null, Guid? conversationId = null, Guid? folderId = null);
}
