namespace SharpOMatic.Engine.Repository;

[Index(nameof(WorkflowId))]
public class Conversation
{
    [Key]
    [MaxLength(256)]
    public required string ConversationId { get; set; }
    public required Guid WorkflowId { get; set; }
    public required ConversationStatus Status { get; set; }
    public required DateTime Created { get; set; }
    public required DateTime Updated { get; set; }
    public int CurrentTurnNumber { get; set; }
    public Guid? LastRunId { get; set; }
    public string? LastError { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpires { get; set; }
}
