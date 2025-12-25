namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RunController : ControllerBase
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [HttpGet("latestforworkflow/{id}")]
    public async Task<Run?> GetLatestRunForWorkflow(IRepository repository, Guid id)
    {
        var run = await (from r in repository.GetWorkflowRuns(id)
                         orderby r.Created descending
                         select r).FirstOrDefaultAsync();

        if (run is not null)
            NormalizeInputEntriesForClient(run);

        return run;
    }

    [HttpGet("latestforworkflow/{id}/{page}/{count}")]
    public async Task<WorkflowRunPageResult> GetLatestRunsForWorkflow(IRepository repository, Guid id, int page, int count)
    {
        var totalCount = await repository.GetWorkflowRuns(id).CountAsync();
        if (count < 1 || page < 1 || totalCount == 0)
            return new WorkflowRunPageResult([], totalCount);

        var skip = (page - 1) * count;
        var runs = await (from r in repository.GetWorkflowRuns(id)
                          orderby r.Created descending
                          select r).Skip(skip).Take(count).ToListAsync();

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
