namespace SharpOMatic.Engine.Repository;

public class EvalRun
{
    [Key]
    public required Guid EvalRunId { get; set; }
    public required Guid EvalConfigId { get; set; }
    public required string Name { get; set; }
    public required DateTime Started { get; set; }
    public DateTime? Finished { get; set; }
    public required EvalRunStatus Status { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public required bool CancelRequested { get; set; }
    public required int TotalRows { get; set; }
    public required int CompletedRows { get; set; }
    public required int FailedRows { get; set; }
}
