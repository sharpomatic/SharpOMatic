namespace SharpOMatic.Engine.Repository;

[Index(nameof(RunId), nameof(SequenceNumber), IsUnique = true)]
[Index(nameof(ConversationId), nameof(SequenceNumber))]
[Index(nameof(WorkflowId), nameof(Created))]
public class StreamEvent
{
    [Key]
    public required Guid StreamEventId { get; set; }
    public required Guid RunId { get; set; }
    public required Guid WorkflowId { get; set; }
    public Guid? ConversationId { get; set; }
    public required int SequenceNumber { get; set; }
    public required DateTime Created { get; set; }
    public required StreamEventKind EventKind { get; set; }
    public Guid? MessageId { get; set; }
    public StreamMessageRole? MessageRole { get; set; }
    public string? TextDelta { get; set; }
    public string? Metadata { get; set; }
}
