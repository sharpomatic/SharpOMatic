namespace SharpOMatic.Engine.DTO;

public class EvalRunDetail
{
    public required EvalRunSummary EvalRun { get; set; }
    public required List<EvalRunGraderSummaryDetail> GraderSummaries { get; set; }
}
