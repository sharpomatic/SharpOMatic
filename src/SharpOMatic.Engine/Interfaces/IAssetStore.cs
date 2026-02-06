namespace SharpOMatic.Engine.Interfaces;

public interface IAssetStore
{
    Task SaveAsync(
        string storageKey,
        Stream content,
        CancellationToken cancellationToken = default
    );
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
