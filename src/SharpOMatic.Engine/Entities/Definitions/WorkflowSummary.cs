namespace SharpOMatic.Engine.Entities.Definitions;

public class WorkflowSummary : Entity
{
    public Guid? WorkflowFolderId { get; set; }
    public string? WorkflowFolderName { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required bool IsConversationEnabled { get; set; }
}
