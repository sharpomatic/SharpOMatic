namespace SharpOMatic.Engine.Repository;

[Index(nameof(Scope), nameof(FolderId), nameof(Created))]
[Index(nameof(Scope), nameof(Created))]
[Index(nameof(RunId))]
[Index(nameof(Name))]
public class Asset
{
    [Key]
    public required Guid AssetId { get; set; }
    public required Guid? RunId { get; set; }
    public required Guid? FolderId { get; set; }
    public required string Name { get; set; }
    public required AssetScope Scope { get; set; }
    public required DateTime Created { get; set; }
    public required string MediaType { get; set; }
    public required long SizeBytes { get; set; }
    public required string StorageKey { get; set; }
}
