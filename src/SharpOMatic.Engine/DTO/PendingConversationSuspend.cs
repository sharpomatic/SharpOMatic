namespace SharpOMatic.Engine.DTO;

public sealed class PendingConversationSuspend
{
    public required Guid ResumeNodeId { get; init; }
    public required string ContextJson { get; init; }
    public string? ResumeStateJson { get; init; }
    public required string WorkflowSnapshotsJson { get; init; }
    public required string GosubStackJson { get; init; }
    public required string Message { get; init; }
}
