namespace SharpOMatic.Engine.Repository;

[Index(nameof(Name), IsUnique = true)]
public class AssetFolder
{
    [Key]
    public required Guid FolderId { get; set; }
    public required string Name { get; set; }
    public required DateTime Created { get; set; }
}
