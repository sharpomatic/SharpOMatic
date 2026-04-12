namespace SharpOMatic.Engine.DTO;

public sealed class ConversationStartRequest
{
    public required Guid WorkflowId { get; set; }
    public required string ConversationId { get; set; }
    public NodeResumeInput? ResumeInput { get; set; }
    public ContextEntryListEntity? InputEntries { get; set; }
    public bool NeedsEditorEvents { get; set; }
}
