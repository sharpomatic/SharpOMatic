using SharpOMatic.Engine.Repository;

namespace SharpOMatic.Engine.DataTransferObjects;

public record class WorkflowRunPageResult(List<Run> Runs, int TotalCount);
