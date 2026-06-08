namespace SharpOMatic.Engine.Entities.Definitions;

public class WorkflowEntity : Entity
{
    public string? FolderName { get; set; }
    public Guid? WorkflowFolderId { get; set; }
    public string? WorkflowFolderName { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required NodeEntity[] Nodes { get; set; }
    public required ConnectionEntity[] Connections { get; set; }
    public bool IsConversationEnabled { get; set; }
}
