namespace SharpOMatic.Engine.Repository;

[Index(nameof(RunId), nameof(Created))]
public class Trace
{
    [Key]
    public required Guid TraceId { get; set; }
    public required Guid RunId { get; set; }
    public required Guid WorkflowId { get; set; }
    public required Guid NodeEntityId { get; set; }
    public required Guid? ParentNodeEntityId { get; set; }
    public required int ThreadId { get; set; }
    public required DateTime Created { get; set; }
    public required NodeType NodeType { get; set; }
    public required NodeStatus NodeStatus { get; set; }
    public required string Title { get; set; }
    public DateTime? Finished { get; set; }
    public string? InputContext { get; set; }
    public string? OutputContext { get; set; }
    public string? CustomData { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
