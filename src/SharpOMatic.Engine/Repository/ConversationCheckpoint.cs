namespace SharpOMatic.Engine.Repository;

public class ConversationCheckpoint
{
    [Key]
    [MaxLength(256)]
    public required string ConversationId { get; set; }
    public required ConversationResumeMode ResumeMode { get; set; }
    public Guid? ResumeNodeId { get; set; }
    public string? ContextJson { get; set; }
    public string? ResumeStateJson { get; set; }
    public string? WorkflowSnapshotsJson { get; set; }
    public string? GosubStackJson { get; set; }
    public required DateTime CheckpointCreated { get; set; }
    public Guid? SourceRunId { get; set; }
}
