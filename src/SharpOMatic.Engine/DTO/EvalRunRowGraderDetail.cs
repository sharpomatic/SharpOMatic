namespace SharpOMatic.Engine.DTO;

public class EvalRunRowGraderDetail
{
    public required Guid EvalRunRowGraderId { get; set; }
    public required Guid EvalRunRowId { get; set; }
    public required Guid EvalGraderId { get; set; }
    public required string Label { get; set; }
    public required int Order { get; set; }
    public required EvalRunStatus Status { get; set; }
    public required DateTime Started { get; set; }
    public DateTime? Finished { get; set; }
    public double? Score { get; set; }
    public string? OutputContext { get; set; }
    public string? Error { get; set; }
}
