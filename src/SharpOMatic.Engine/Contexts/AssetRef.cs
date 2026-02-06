namespace SharpOMatic.Engine.Contexts;

public class AssetRef
{
    public AssetRef() { }

    public AssetRef(Asset asset)
    {
        if (asset is null)
            throw new ArgumentNullException(nameof(asset));

        AssetId = asset.AssetId;
        Name = asset.Name;
        MediaType = asset.MediaType;
        SizeBytes = asset.SizeBytes;
    }

    public required Guid AssetId { get; set; }
    public required string Name { get; set; }
    public required string MediaType { get; set; }
    public required long SizeBytes { get; set; }
}
