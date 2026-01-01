namespace SharpOMatic.Engine.Services;

public sealed class AzureBlobStorageAssetStoreOptions
{
    public string? ConnectionString { get; set; }
    public string? ServiceUri { get; set; }
    public string ContainerName { get; set; } = string.Empty;
}
