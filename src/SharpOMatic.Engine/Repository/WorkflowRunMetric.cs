namespace SharpOMatic.Engine.Repository;

[Index(nameof(Created))]
[Index(nameof(WorkflowId), nameof(Created))]
[Index(nameof(Succeeded), nameof(Created))]
[Index(nameof(RunStatus), nameof(Created))]
[Index(nameof(ConversationId), nameof(Created))]
[Index(nameof(RunId), IsUnique = true)]
public class WorkflowRunMetric
{
    [Key]
    public required Guid Id { get; set; }
    public required DateTime Created { get; set; }
    public required DateTime Started { get; set; }
    public required DateTime Finished { get; set; }
    public long? Duration { get; set; }
    public required Guid RunId { get; set; }
    public required Guid WorkflowId { get; set; }
    public required string WorkflowName { get; set; }
    public required int WorkflowVersion { get; set; }
    public required bool Succeeded { get; set; }
    public required RunStatus RunStatus { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? FailedNodeEntityId { get; set; }
    public string? FailedNodeTitle { get; set; }
    public NodeType? FailedNodeType { get; set; }
    [MaxLength(256)]
    public string? ConversationId { get; set; }
    public int? TurnNumber { get; set; }
    public required bool IsConversationRun { get; set; }
    public int ModelCallCount { get; set; }
    public int ModelCallFailureCount { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long TotalTokens { get; set; }
    public decimal TotalModelCost { get; set; }
}
