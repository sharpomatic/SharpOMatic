namespace SharpOMatic.Engine.Repository;

[Index(nameof(Name), IsUnique = true)]
public class WorkflowFolder
{
    [Key]
    public required Guid WorkflowFolderId { get; set; }
    public required string Name { get; set; }
    public required DateTime Created { get; set; }
}
