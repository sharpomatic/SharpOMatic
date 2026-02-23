namespace SharpOMatic.Engine.Contexts;

public class AssetRef
{
    public AssetRef() { }

    [SetsRequiredMembers]
    public AssetRef(Asset asset)
    {
        if (asset is null)
            throw new ArgumentNullException(nameof(asset));

        AssetId = asset.AssetId;
        Name = asset.Name;
        MediaType = asset.MediaType;
        SizeBytes = asset.SizeBytes;
        FolderId = asset.FolderId;
    }

    public required Guid AssetId { get; set; }
    public required string Name { get; set; }
    public required string MediaType { get; set; }
    public required long SizeBytes { get; set; }
    public Guid? FolderId { get; set; }
    public string? FolderName { get; set; }
}
