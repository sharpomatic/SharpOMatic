namespace SharpOMatic.Engine.Repository;

public class Run
{
    [Key]
    public required Guid RunId { get; set; }
    public required Guid WorkflowId { get; set; }
    public required DateTime Created { get; set; }
    public required RunStatus RunStatus { get; set; }
    public DateTime? Started { get; set; }
    public DateTime? Stopped { get; set; }
    public string? InputEntries { get; set; }
    public string? InputContext { get; set; } 
    public string? OutputContext { get; set; }
    public string? CustomData { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
