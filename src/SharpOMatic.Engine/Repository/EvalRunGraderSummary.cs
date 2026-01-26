namespace SharpOMatic.Engine.Repository;

public class EvalRunGraderSummary
{
    [Key]
    public required Guid EvalRunGraderSummaryId { get; set; }
    public required Guid EvalRunId { get; set; }
    public required Guid EvalGraderId { get; set; }
    public required int TotalCount { get; set; }
    public required int CompletedCount { get; set; }
    public required int FailedCount { get; set; }
    public double? MinScore { get; set; }
    public double? MaxScore { get; set; }
    public double? AverageScore { get; set; }
    public double? MedianScore { get; set; }
    public double? StandardDeviation { get; set; }
    public double? PassRate { get; set; }
}
