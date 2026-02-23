namespace SharpOMatic.Engine.Interfaces;

public interface IAssetStoreMove
{
    Task MoveAsync(string sourceStorageKey, string destinationStorageKey, bool overwrite = true, CancellationToken cancellationToken = default);
}
