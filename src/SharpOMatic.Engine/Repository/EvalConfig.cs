namespace SharpOMatic.Engine.Repository;

public class EvalConfig
{
    [Key]
    public required Guid EvalConfigId { get; set; }
    public required Guid? WorkflowId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required int MaxParallel { get; set; }
}
