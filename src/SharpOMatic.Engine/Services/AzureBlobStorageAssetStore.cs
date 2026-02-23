namespace SharpOMatic.Engine.Services;

public class AzureBlobStorageAssetStore : IAssetStore, IAssetStoreMove
{
    private readonly BlobContainerClient _containerClient;
    private readonly SemaphoreSlim _containerInit = new(1, 1);
    private bool _containerEnsured;

    public AzureBlobStorageAssetStore(IOptions<AzureBlobStorageAssetStoreOptions> options)
    {
        if (options is null)
            throw new SharpOMaticException("Azure Blob Storage options are required.");

        var settings = options.Value ?? new AzureBlobStorageAssetStoreOptions();
        var containerName = settings.ContainerName?.Trim();
        if (string.IsNullOrWhiteSpace(containerName))
            throw new SharpOMaticException("Azure Blob Storage container name is required.");

        var hasConnectionString = !string.IsNullOrWhiteSpace(settings.ConnectionString);
        var hasServiceUri = !string.IsNullOrWhiteSpace(settings.ServiceUri);

        if (hasConnectionString == hasServiceUri)
            throw new SharpOMaticException("Specify exactly one of ConnectionString or ServiceUri for Azure Blob Storage.");

        if (hasConnectionString)
            _containerClient = new BlobContainerClient(settings.ConnectionString, containerName);
        else
        {
            if (!Uri.TryCreate(settings.ServiceUri, UriKind.Absolute, out var serviceUri))
                throw new SharpOMaticException("Azure Blob Storage ServiceUri is invalid.");

            var serviceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
            _containerClient = serviceClient.GetBlobContainerClient(containerName);
        }
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        if (content is null)
            throw new SharpOMaticException("Asset content is required.");

        await EnsureContainerAsync(cancellationToken);
        var blobClient = GetBlobClient(storageKey);
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken: cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var blobClient = GetBlobClient(storageKey);
        var exists = await blobClient.ExistsAsync(cancellationToken);
        if (!exists.Value)
            throw new SharpOMaticException($"Asset '{storageKey}' cannot be found.");

        return await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var blobClient = GetBlobClient(storageKey);
        var exists = await blobClient.ExistsAsync(cancellationToken);
        return exists.Value;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var blobClient = GetBlobClient(storageKey);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task MoveAsync(string sourceStorageKey, string destinationStorageKey, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var sourceBlobClient = GetBlobClient(sourceStorageKey);
        var destinationBlobClient = GetBlobClient(destinationStorageKey);

        if (!await sourceBlobClient.ExistsAsync(cancellationToken))
            throw new SharpOMaticException($"Asset '{sourceStorageKey}' cannot be found.");

        if (!overwrite && await destinationBlobClient.ExistsAsync(cancellationToken))
            throw new SharpOMaticException($"Asset '{destinationStorageKey}' already exists.");

        await destinationBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri, cancellationToken: cancellationToken);

        while (true)
        {
            var properties = await destinationBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var copyStatus = properties.Value.CopyStatus;
            if (copyStatus == Azure.Storage.Blobs.Models.CopyStatus.Success)
                break;

            if (copyStatus != Azure.Storage.Blobs.Models.CopyStatus.Pending)
                throw new SharpOMaticException($"Unable to move asset content from '{sourceStorageKey}' to '{destinationStorageKey}'.");

            await Task.Delay(150, cancellationToken);
        }

        await sourceBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private BlobClient GetBlobClient(string storageKey)
    {
        var segments = AssetStorageKey.GetSegments(storageKey);
        var blobName = string.Join('/', segments);
        return _containerClient.GetBlobClient(blobName);
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
            return;

        await _containerInit.WaitAsync(cancellationToken);

        try
        {
            if (_containerEnsured)
                return;

            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _containerEnsured = true;
        }
        finally
        {
            _containerInit.Release();
        }
    }
}

public class AzureBlobStorageAssetStoreOptions
{
    public string? ConnectionString { get; set; }
    public string? ServiceUri { get; set; }
    public string ContainerName { get; set; } = string.Empty;
}
