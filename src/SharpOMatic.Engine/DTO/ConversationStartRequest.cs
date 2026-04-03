namespace SharpOMatic.Engine.DTO;

public sealed class ConversationStartRequest
{
    public required Guid WorkflowId { get; set; }
    public required Guid ConversationId { get; set; }
    public NodeResumeInput? ResumeInput { get; set; }
    public bool NeedsEditorEvents { get; set; }
}
