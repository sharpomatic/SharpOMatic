namespace SharpOMatic.Engine.Repository;

public class EvalRunRowGrader
{
    [Key]
    public required Guid EvalRunRowGraderId { get; set; }
    public required Guid EvalRunRowId { get; set; }
    public required Guid EvalGraderId { get; set; }
    public required Guid EvalRunId { get; set; }
    public required DateTime Started { get; set; }
    public DateTime? Finished { get; set; }
    public required EvalRunStatus Status { get; set; }
    public double? Score { get; set; }
    public string? OutputContext { get; set; }
    public string? Error { get; set; }
}
