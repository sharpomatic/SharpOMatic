namespace SharpOMatic.Engine.Services;

public sealed class FileSystemAssetStore : IAssetStore
{
    private readonly string _rootPath;

    public FileSystemAssetStore(IOptions<FileSystemAssetStoreOptions> options)
    {
        var configuredRoot = options.Value?.RootPath;
        var rootPath = string.IsNullOrWhiteSpace(configuredRoot) ? FileSystemAssetStoreOptions.DefaultRootPath : configuredRoot;
        _rootPath = Path.GetFullPath(rootPath);
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new SharpOMaticException($"Storage key '{storageKey}' is invalid.");

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                                     81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await content.CopyToAsync(output, cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
            throw new SharpOMaticException($"Asset '{storageKey}' cannot be found.");

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                       81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        return Task.FromResult(File.Exists(path));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);

        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var segments = AssetStorageKey.GetSegments(storageKey);
        var rootPath = _rootPath;
        var path = rootPath;

        foreach (var segment in segments)
            path = Path.Combine(path, segment);

        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(rootPath, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new SharpOMaticException($"Storage key '{storageKey}' resolves outside of the asset root.");

        return fullPath;
    }
}
