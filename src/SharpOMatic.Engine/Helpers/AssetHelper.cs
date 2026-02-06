namespace SharpOMatic.Engine.Helpers;

public class AssetHelper
{
    private static readonly HttpClient SharedHttpClient = new();
    private readonly IRepositoryService _repositoryService;
    private readonly IAssetStore _assetStore;
    private readonly Guid? _runId;

    public AssetHelper(
        IRepositoryService repositoryService,
        IAssetStore assetStore,
        Guid? runId = null
    )
    {
        _repositoryService =
            repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _assetStore = assetStore ?? throw new ArgumentNullException(nameof(assetStore));
        _runId = runId;
    }

    public Task<byte[]> LoadAssetBytesAsync(Asset asset)
    {
        if (asset is null)
            throw new ArgumentNullException(nameof(asset));

        return LoadAssetBytesInternal(asset);
    }

    public async Task<byte[]> LoadAssetBytesAsync(AssetRef assetRef)
    {
        if (assetRef is null)
            throw new ArgumentNullException(nameof(assetRef));

        if (assetRef.AssetId == Guid.Empty)
            throw new SharpOMaticException("AssetRef must include a valid assetId.");

        var asset = await _repositoryService.GetAsset(assetRef.AssetId);
        return await LoadAssetBytesInternal(asset);
    }

    public async Task<byte[]> LoadAssetBytesAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            throw new SharpOMaticException("AssetId must be a non-empty GUID.");

        var asset = await _repositoryService.GetAsset(assetId);
        return await LoadAssetBytesInternal(asset);
    }

    public async Task<byte[]> LoadAssetBytesAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new SharpOMaticException("Asset name cannot be empty or whitespace.");

        Asset? asset = null;

        if (_runId.HasValue)
            asset = await _repositoryService.GetRunAssetByName(_runId.Value, name);

        asset ??= await _repositoryService.GetLibraryAssetByName(name);

        if (asset is null)
            throw new SharpOMaticException($"Asset '{name}' cannot be found.");

        return await LoadAssetBytesInternal(asset);
    }

    public Task<Stream> LoadAssetStreamAsync(Asset asset)
    {
        if (asset is null)
            throw new ArgumentNullException(nameof(asset));

        return LoadAssetStreamInternal(asset);
    }

    public async Task<Stream> LoadAssetStreamAsync(AssetRef assetRef)
    {
        if (assetRef is null)
            throw new ArgumentNullException(nameof(assetRef));

        if (assetRef.AssetId == Guid.Empty)
            throw new SharpOMaticException("AssetRef must include a valid assetId.");

        var asset = await _repositoryService.GetAsset(assetRef.AssetId);
        return await LoadAssetStreamInternal(asset);
    }

    public async Task<Stream> LoadAssetStreamAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            throw new SharpOMaticException("AssetId must be a non-empty GUID.");

        var asset = await _repositoryService.GetAsset(assetId);
        return await LoadAssetStreamInternal(asset);
    }

    public async Task<Stream> LoadAssetStreamAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new SharpOMaticException("Asset name cannot be empty or whitespace.");

        Asset? asset = null;

        if (_runId.HasValue)
            asset = await _repositoryService.GetRunAssetByName(_runId.Value, name);

        asset ??= await _repositoryService.GetLibraryAssetByName(name);

        if (asset is null)
            throw new SharpOMaticException($"Asset '{name}' cannot be found.");

        return await LoadAssetStreamInternal(asset);
    }

    public Task<string> LoadAssetTextAsync(Asset asset, Encoding? encoding = null)
    {
        if (asset is null)
            throw new ArgumentNullException(nameof(asset));

        return LoadAssetTextInternal(asset, encoding);
    }

    public async Task<string> LoadAssetTextAsync(AssetRef assetRef, Encoding? encoding = null)
    {
        if (assetRef is null)
            throw new ArgumentNullException(nameof(assetRef));

        if (assetRef.AssetId == Guid.Empty)
            throw new SharpOMaticException("AssetRef must include a valid assetId.");

        var asset = await _repositoryService.GetAsset(assetRef.AssetId);
        return await LoadAssetTextInternal(asset, encoding);
    }

    public async Task<string> LoadAssetTextAsync(Guid assetId, Encoding? encoding = null)
    {
        if (assetId == Guid.Empty)
            throw new SharpOMaticException("AssetId must be a non-empty GUID.");

        var asset = await _repositoryService.GetAsset(assetId);
        return await LoadAssetTextInternal(asset, encoding);
    }

    public async Task<string> LoadAssetTextAsync(string name, Encoding? encoding = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new SharpOMaticException("Asset name cannot be empty or whitespace.");

        Asset? asset = null;

        if (_runId.HasValue)
            asset = await _repositoryService.GetRunAssetByName(_runId.Value, name);

        asset ??= await _repositoryService.GetLibraryAssetByName(name);

        if (asset is null)
            throw new SharpOMaticException($"Asset '{name}' cannot be found.");

        return await LoadAssetTextInternal(asset, encoding);
    }

    public async Task<AssetRef> AddAssetFromBytesAsync(
        byte[] data,
        string name,
        string mediaType,
        AssetScope scope = AssetScope.Run
    )
    {
        if (data is null)
            throw new SharpOMaticException("Asset data is required.");

        await using var stream = new MemoryStream(data, writable: false);
        return await AddAssetFromStreamInternal(stream, name, mediaType, scope);
    }

    public Task<AssetRef> AddAssetFromStreamAsync(
        Stream content,
        string name,
        string mediaType,
        AssetScope scope = AssetScope.Run
    )
    {
        return AddAssetFromStreamInternal(content, name, mediaType, scope);
    }

    public async Task<AssetRef> AddAssetFromBase64Async(
        string base64,
        string name,
        string mediaType,
        AssetScope scope = AssetScope.Run
    )
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new SharpOMaticException("Asset base64 is required.");

        try
        {
            var data = Convert.FromBase64String(base64);
            return await AddAssetFromBytesAsync(data, name, mediaType, scope);
        }
        catch (FormatException)
        {
            throw new SharpOMaticException("Asset base64 is invalid.");
        }
    }

    public async Task<AssetRef> AddAssetFromFileAsync(
        string filePath,
        string name,
        string mediaType,
        AssetScope scope = AssetScope.Run
    )
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new SharpOMaticException("Asset file path is required.");

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new SharpOMaticException($"Asset file '{fullPath}' cannot be found.");

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        return await AddAssetFromStreamInternal(stream, name, mediaType, scope);
    }

    public async Task<AssetRef> AddAssetFromUriAsync(
        Uri uri,
        string name,
        string mediaType,
        AssetScope scope = AssetScope.Run
    )
    {
        if (uri is null)
            throw new SharpOMaticException("Asset uri is required.");

        if (!uri.IsAbsoluteUri)
            throw new SharpOMaticException("Asset uri must be absolute.");

        using var response = await SharedHttpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead
        );
        if (!response.IsSuccessStatusCode)
            throw new SharpOMaticException(
                $"Asset download failed with status code {(int)response.StatusCode} ({response.StatusCode})."
            );

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await AddAssetFromStreamInternal(stream, name, mediaType, scope);
    }

    public Task DeleteAssetAsync(Asset asset)
    {
        if (asset is null)
            throw new ArgumentNullException(nameof(asset));

        return DeleteAssetInternal(asset);
    }

    public async Task DeleteAssetAsync(AssetRef assetRef)
    {
        if (assetRef is null)
            throw new ArgumentNullException(nameof(assetRef));

        if (assetRef.AssetId == Guid.Empty)
            throw new SharpOMaticException("AssetRef must include a valid assetId.");

        var asset = await _repositoryService.GetAsset(assetRef.AssetId);
        await DeleteAssetInternal(asset);
    }

    public async Task DeleteAssetAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            throw new SharpOMaticException("AssetId must be a non-empty GUID.");

        var asset = await _repositoryService.GetAsset(assetId);
        await DeleteAssetInternal(asset);
    }

    public async Task DeleteAssetAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new SharpOMaticException("Asset name cannot be empty or whitespace.");

        Asset? asset = null;

        if (_runId.HasValue)
            asset = await _repositoryService.GetRunAssetByName(_runId.Value, name);

        asset ??= await _repositoryService.GetLibraryAssetByName(name);

        if (asset is null)
            throw new SharpOMaticException($"Asset '{name}' cannot be found.");

        await DeleteAssetInternal(asset);
    }

    private async Task<byte[]> LoadAssetBytesInternal(Asset asset)
    {
        await using var stream = await _assetStore.OpenReadAsync(asset.StorageKey);

        using var buffer =
            asset.SizeBytes > 0 && asset.SizeBytes <= int.MaxValue
                ? new MemoryStream((int)asset.SizeBytes)
                : new MemoryStream();

        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private Task<Stream> LoadAssetStreamInternal(Asset asset)
    {
        return _assetStore.OpenReadAsync(asset.StorageKey);
    }

    private async Task<string> LoadAssetTextInternal(Asset asset, Encoding? encoding)
    {
        var bytes = await LoadAssetBytesInternal(asset);
        return (encoding ?? Encoding.UTF8).GetString(bytes);
    }

    private async Task<AssetRef> AddAssetFromStreamInternal(
        Stream content,
        string name,
        string mediaType,
        AssetScope scope
    )
    {
        if (content is null)
            throw new SharpOMaticException("Asset content is required.");

        if (!content.CanRead)
            throw new SharpOMaticException("Asset content must be readable.");

        if (content.CanSeek && content.Position != 0)
            content.Position = 0;

        if (string.IsNullOrWhiteSpace(name))
            throw new SharpOMaticException("Asset name is required.");

        if (string.IsNullOrWhiteSpace(mediaType))
            throw new SharpOMaticException("Asset media type is required.");

        if (scope == AssetScope.Run && !_runId.HasValue)
            throw new SharpOMaticException("Run assets require a runId.");

        var assetId = Guid.NewGuid();
        var storageKey = AssetStorageKey.ForScope(scope, assetId, _runId);

        using var countingStream = new CountingStream(content, leaveOpen: true);
        await _assetStore.SaveAsync(storageKey, countingStream);

        var asset = new Asset
        {
            AssetId = assetId,
            RunId = scope == AssetScope.Run ? _runId : null,
            Name = name.Trim(),
            Scope = scope,
            Created = DateTime.Now,
            MediaType = mediaType.Trim(),
            SizeBytes = countingStream.BytesRead,
            StorageKey = storageKey,
        };

        await _repositoryService.UpsertAsset(asset);

        return new AssetRef
        {
            AssetId = asset.AssetId,
            Name = asset.Name,
            MediaType = asset.MediaType,
            SizeBytes = asset.SizeBytes,
        };
    }

    private async Task DeleteAssetInternal(Asset asset)
    {
        await _repositoryService.DeleteAsset(asset.AssetId);
        await _assetStore.DeleteAsync(asset.StorageKey);
    }

    private sealed class CountingStream : Stream
    {
        private readonly Stream _inner;
        private readonly bool _leaveOpen;

        public CountingStream(Stream inner, bool leaveOpen)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _leaveOpen = leaveOpen;
        }

        public long BytesRead { get; private set; }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            BytesRead += read;
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => _inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        ) => _inner.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
                _inner.Dispose();

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_leaveOpen)
                await _inner.DisposeAsync();

            await base.DisposeAsync();
        }
    }
}
