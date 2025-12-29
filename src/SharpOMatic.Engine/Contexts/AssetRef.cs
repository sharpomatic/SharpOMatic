namespace SharpOMatic.Engine.Contexts;

public class AssetRef
{
    public required Guid AssetId { get; set; }
    public required string Name { get; set; }
    public required string MediaType { get; set; }
    public required long SizeBytes { get; set; }
}
