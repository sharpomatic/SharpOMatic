namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RunController : ControllerBase
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [HttpGet("latestforworkflow/{id}")]
    public async Task<Run?> GetLatestRunForWorkflow(IRepositoryService repositoryService, Guid id)
    {
        var run = await repositoryService.GetLatestRunForWorkflow(id);

        if (run is not null)
            NormalizeInputEntriesForClient(run);

        return run;
    }

    [HttpGet("latestforworkflow/{id}/{page}/{count}")]
    public async Task<WorkflowRunPageResult> GetLatestRunsForWorkflow(
        IRepositoryService repositoryService,
        Guid id,
        int page,
        int count,
        [FromQuery] RunSortField sortBy = RunSortField.Created,
        [FromQuery] SortDirection sortDirection = SortDirection.Descending
    )
    {
        var totalCount = await repositoryService.GetWorkflowRunCount(id);
        if (count < 1 || page < 1 || totalCount == 0)
            return new WorkflowRunPageResult([], totalCount);

        var skip = (page - 1) * count;
        var runs = await repositoryService.GetWorkflowRuns(id, sortBy, sortDirection, skip, count);

        foreach (var run in runs)
            NormalizeInputEntriesForClient(run);

        return new WorkflowRunPageResult(runs, totalCount);
    }

    private static void NormalizeInputEntriesForClient(Run run)
    {
        if (run.InputEntries is null)
            return;

        // Convert to camelCase which is used by the front end
        var entries = JsonSerializer.Deserialize<ContextEntryListEntity>(run.InputEntries);
        run.InputEntries = JsonSerializer.Serialize(entries, _options);
    }
}
