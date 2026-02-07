namespace SharpOMatic.Engine.DTO;

public class EvalRunRowDetail
{
    public required Guid EvalRunRowId { get; set; }
    public required Guid EvalRunId { get; set; }
    public required Guid EvalRowId { get; set; }
    public required string Name { get; set; }
    public required int Order { get; set; }
    public required EvalRunStatus Status { get; set; }
    public required DateTime Started { get; set; }
    public DateTime? Finished { get; set; }
    public string? OutputContext { get; set; }
    public string? Error { get; set; }
    public required List<EvalRunRowGraderDetail> Graders { get; set; }
}
