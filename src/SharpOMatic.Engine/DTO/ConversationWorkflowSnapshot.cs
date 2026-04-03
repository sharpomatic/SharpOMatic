namespace SharpOMatic.Engine.DTO;

public sealed class ConversationWorkflowSnapshot
{
    public required Guid WorkflowId { get; init; }
    public required string WorkflowJson { get; init; }
}
