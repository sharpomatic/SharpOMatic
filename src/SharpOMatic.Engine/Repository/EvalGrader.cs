namespace SharpOMatic.Engine.Repository;

public class EvalGrader
{
    [Key]
    public required Guid EvalGraderId { get; set; }
    public required Guid EvalConfigId { get; set; }
    public required Guid? WorkflowId { get; set; }
    public required int Order { get; set; }
    public required string Label { get; set; }
    public required double PassThreshold { get; set; }
}
