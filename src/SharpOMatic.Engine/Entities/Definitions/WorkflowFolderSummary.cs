namespace SharpOMatic.Engine.Entities.Definitions;

public class WorkflowFolderSummary
{
    public required Guid WorkflowFolderId { get; set; }
    public required string Name { get; set; }
    public required DateTime Created { get; set; }
}
