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
        [FromQuery] int take = 0)
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
}
