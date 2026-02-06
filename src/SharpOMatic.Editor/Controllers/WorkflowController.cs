namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new NodeEntityConverter() },
    };

    [HttpGet]
    public Task<List<WorkflowSummary>> GetWorkflowSummaries(
        IRepositoryService repository,
        [FromQuery] string? search = null,
        [FromQuery] WorkflowSortField sortBy = WorkflowSortField.Name,
        [FromQuery] SortDirection sortDirection = SortDirection.Ascending,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 0
    )
    {
        if (skip < 0)
            skip = 0;

        if (take < 0)
            take = 0;

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return repository.GetWorkflowSummaries(normalizedSearch, sortBy, sortDirection, skip, take);
    }

    [HttpGet("count")]
    public Task<int> GetWorkflowSummaryCount(
        IRepositoryService repository,
        [FromQuery] string? search = null
    )
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return repository.GetWorkflowSummaryCount(normalizedSearch);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowEntity>> GetWorkflow(
        IRepositoryService repository,
        Guid id
    )
    {
        return await repository.GetWorkflow(id);
    }

    [HttpPost()]
    public async Task UpsertWorkflow(IRepositoryService repository)
    {
        string requestBody;
        using var reader = new StreamReader(Request.Body);
        requestBody = await reader.ReadToEndAsync();
        var workflow = JsonSerializer.Deserialize<WorkflowEntity>(requestBody, _options);
        await repository.UpsertWorkflow(workflow!);
    }

    [HttpDelete("{id}")]
    public async Task DeleteWorkflow(IRepositoryService repository, Guid id)
    {
        await repository.DeleteWorkflow(id);
    }

    [HttpPost("copy/{id}")]
    public async Task<ActionResult<Guid>> CopyWorkflow(IRepositoryService repository, Guid id)
    {
        return await repository.CopyWorkflow(id);
    }

    [HttpPost("run/{id}")]
    public async Task<ActionResult<Guid>> Run(IEngineService engineService, Guid id)
    {
        // Parse the incoming ContextEntryListEntity data
        using var reader = new StreamReader(Request.Body);
        var contextEntryListEntity = JsonSerializer.Deserialize<ContextEntryListEntity>(
            await reader.ReadToEndAsync(),
            _options
        );

        var runId = await engineService.CreateWorkflowRun(id);
        await engineService.StartWorkflowRunAndNotify(runId, inputEntries: contextEntryListEntity);
        return runId;
    }
}
