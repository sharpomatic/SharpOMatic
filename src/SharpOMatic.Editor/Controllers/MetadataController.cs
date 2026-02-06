namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetadataController(IRepositoryService repositoryService) : ControllerBase
{
    [HttpGet("connector-configs")]
    public async Task<IEnumerable<ConnectorConfig>> GetConnectorConfigs()
    {
        return await repositoryService.GetConnectorConfigs();
    }

    [HttpGet("connectors")]
    public Task<List<ConnectorSummary>> GetConnectorSummaries(
        IRepositoryService repositoryService,
        [FromQuery] string? search = null,
        [FromQuery] ConnectorSortField sortBy = ConnectorSortField.Name,
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
        return repositoryService.GetConnectorSummaries(normalizedSearch, sortBy, sortDirection, skip, take);
    }

    [HttpGet("connectors/count")]
    public Task<int> GetConnectorSummaryCount(IRepositoryService repositoryService, [FromQuery] string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return repositoryService.GetConnectorSummaryCount(normalizedSearch);
    }

    [HttpGet("connectors/{id}")]
    public async Task<ActionResult<Connector>> GetConnector(IRepositoryService repositoryService, Guid id)
    {
        return await repositoryService.GetConnector(id);
    }

    [HttpPost("connectors")]
    public async Task UpsertConnector(IRepositoryService repositoryService, [FromBody] Connector connector)
    {
        await repositoryService.UpsertConnector(connector);
    }

    [HttpDelete("connectors/{id}")]
    public async Task DeleteConnector(IRepositoryService repositoryService, Guid id)
    {
        await repositoryService.DeleteConnector(id);
    }

    [HttpGet("model-configs")]
    public async Task<IEnumerable<ModelConfig>> GetModelConfigs()
    {
        return await repositoryService.GetModelConfigs();
    }

    [HttpGet("models")]
    public Task<List<ModelSummary>> GetModelSummaries(
        IRepositoryService repositoryService,
        [FromQuery] string? search = null,
        [FromQuery] ModelSortField sortBy = ModelSortField.Name,
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
        return repositoryService.GetModelSummaries(normalizedSearch, sortBy, sortDirection, skip, take);
    }

    [HttpGet("models/count")]
    public Task<int> GetModelSummaryCount(IRepositoryService repositoryService, [FromQuery] string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return repositoryService.GetModelSummaryCount(normalizedSearch);
    }

    [HttpGet("models/{id}")]
    public async Task<ActionResult<Model>> GetModel(IRepositoryService repositoryService, Guid id)
    {
        return await repositoryService.GetModel(id);
    }

    [HttpPost("models")]
    public async Task UpsertModel(IRepositoryService repositoryService, [FromBody] Model model)
    {
        await repositoryService.UpsertModel(model);
    }

    [HttpDelete("models/{id}")]
    public async Task DeleteModel(IRepositoryService repositoryService, Guid id)
    {
        await repositoryService.DeleteModel(id);
    }
}
