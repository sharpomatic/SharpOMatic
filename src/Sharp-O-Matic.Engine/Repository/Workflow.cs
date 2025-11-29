namespace SharpOMatic.Engine.Repository;

public class Workflow
{
    [Key]
    public required Guid WorkflowId { get; set; }
    public required string Named { get; set; }
    public required string Description { get; set; }
    public required string Nodes { get; set; }
    public required string Connections { get; set; }
}
