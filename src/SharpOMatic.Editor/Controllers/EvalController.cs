namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EvalController : ControllerBase
{
    [HttpGet("configs")]
    public Task<List<EvalConfigSummary>> GetEvalConfigSummaries(
        IRepositoryService repositoryService,
        [FromQuery] string? search = null,
        [FromQuery] EvalConfigSortField sortBy = EvalConfigSortField.Name,
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
        return repositoryService.GetEvalConfigSummaries(normalizedSearch, sortBy, sortDirection, skip, take);
    }

    [HttpGet("configs/count")]
    public Task<int> GetEvalConfigSummaryCount(IRepositoryService repositoryService, [FromQuery] string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return repositoryService.GetEvalConfigSummaryCount(normalizedSearch);
    }

    [HttpGet("configs/{id}")]
    public async Task<ActionResult<EvalConfig>> GetEvalConfig(IRepositoryService repositoryService, Guid id)
    {
        return await repositoryService.GetEvalConfig(id);
    }

    [HttpGet("configs/{id}/detail")]
    public async Task<ActionResult<EvalConfigDetail>> GetEvalConfigDetail(IRepositoryService repositoryService, Guid id)
    {
        return await repositoryService.GetEvalConfigDetail(id);
    }

    [HttpGet("configs/{id}/runs")]
    public Task<List<EvalRunSummary>> GetEvalRunSummaries(
        IRepositoryService repositoryService,
        Guid id,
        [FromQuery] string? search = null,
        [FromQuery] EvalRunSortField sortBy = EvalRunSortField.Started,
        [FromQuery] SortDirection sortDirection = SortDirection.Descending,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 0
    )
    {
        if (skip < 0)
            skip = 0;

        if (take < 0)
            take = 0;

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return repositoryService.GetEvalRunSummaries(id, normalizedSearch, sortBy, sortDirection, skip, take);
    }

    [HttpGet("configs/{id}/runs/count")]
    public Task<int> GetEvalRunSummaryCount(IRepositoryService repositoryService, Guid id, [FromQuery] string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return repositoryService.GetEvalRunSummaryCount(id, normalizedSearch);
    }

    [HttpGet("runs/{id}/detail")]
    public async Task<ActionResult<EvalRunDetail>> GetEvalRunDetail(IRepositoryService repositoryService, Guid id)
    {
        return await repositoryService.GetEvalRunDetail(id);
    }

    [HttpGet("runs/{id}/rows")]
    public Task<List<EvalRunRowDetail>> GetEvalRunRows(
        IRepositoryService repositoryService,
        Guid id,
        [FromQuery] string? search = null,
        [FromQuery] EvalRunRowSortField sortBy = EvalRunRowSortField.Name,
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
        return repositoryService.GetEvalRunRows(id, normalizedSearch, sortBy, sortDirection, skip, take);
    }

    [HttpGet("runs/{id}/rows/count")]
    public Task<int> GetEvalRunRowCount(IRepositoryService repositoryService, Guid id, [FromQuery] string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return repositoryService.GetEvalRunRowCount(id, normalizedSearch);
    }

    [HttpPost("configs/{id}/runs")]
    public async Task<ActionResult<Guid>> StartEvalRun(IEngineService engineService, Guid id)
    {
        var evalRun = await engineService.StartEvalRun(id);
        return evalRun.EvalRunId;
    }

    [HttpPost("configs")]
    public async Task UpsertEvalConfig(IRepositoryService repositoryService, [FromBody] EvalConfig evalConfig)
    {
        await repositoryService.UpsertEvalConfig(evalConfig);
    }

    [HttpDelete("configs/{id}")]
    public async Task DeleteEvalConfig(IRepositoryService repositoryService, Guid id)
    {
        await repositoryService.DeleteEvalConfig(id);
    }

    [HttpPost("configs/graders")]
    public async Task UpsertEvalGraders(IRepositoryService repositoryService, [FromBody] List<EvalGrader> graders)
    {
        if (graders is null || graders.Count == 0)
            return;

        await repositoryService.UpsertEvalGraders(graders);
    }

    [HttpDelete("graders/{id}")]
    public async Task DeleteEvalGrader(IRepositoryService repositoryService, Guid id)
    {
        await repositoryService.DeleteEvalGrader(id);
    }

    [HttpPost("configs/columns")]
    public async Task UpsertEvalColumns(IRepositoryService repositoryService, [FromBody] List<EvalColumn> columns)
    {
        if (columns is null || columns.Count == 0)
            return;

        await repositoryService.UpsertEvalColumns(columns);
    }

    [HttpDelete("columns/{id}")]
    public async Task DeleteEvalColumn(IRepositoryService repositoryService, Guid id)
    {
        await repositoryService.DeleteEvalColumn(id);
    }

    [HttpPost("configs/rows")]
    public async Task UpsertEvalRows(IRepositoryService repositoryService, [FromBody] List<EvalRow> rows)
    {
        if (rows is null || rows.Count == 0)
            return;

        await repositoryService.UpsertEvalRows(rows);
    }

    [HttpDelete("rows/{id}")]
    public async Task DeleteEvalRow(IRepositoryService repositoryService, Guid id)
    {
        await repositoryService.DeleteEvalRow(id);
    }

    [HttpPost("configs/data")]
    public async Task UpsertEvalData(IRepositoryService repositoryService, [FromBody] List<EvalData> data)
    {
        if (data is null || data.Count == 0)
            return;

        await repositoryService.UpsertEvalData(data);
    }

    [HttpDelete("data/{id}")]
    public async Task DeleteEvalData(IRepositoryService repositoryService, Guid id)
    {
        await repositoryService.DeleteEvalData(id);
    }
}
