namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetadataController(IRepository repository) : ControllerBase
{
    [HttpGet("connector-configs")]
    public async Task<IEnumerable<ConnectorConfig>> GetConnectorConfigs()
    {
        return await repository.GetConnectorConfigs();
    }

    [HttpGet("connectors")]
    public Task<List<ConnectorSummary>> GetConnectorSummaries(IRepository repository)
    {
        return (from c in repository.GetConnectors()
                orderby c.Name
                select new ConnectorSummary()
                {
                    ConnectorId = c.ConnectorId,
                    Name = c.Name,
                    Description = c.Description,
                }).ToListAsync();
    }

    [HttpGet("connectors/{id}")]
    public async Task<ActionResult<Connector>> GetConnector(IRepository repository, Guid id)
    {
        return await repository.GetConnector(id);
    }

    [HttpPost("connectors")]
    public async Task UpsertConnector(IRepository repository, [FromBody]Connector connector)
    {
        await repository.UpsertConnector(connector);
    }

    [HttpDelete("connectors/{id}")]
    public async Task DeleteConnector(IRepository repository, Guid id)
    {
        await repository.DeleteConnector(id);
    }

    [HttpGet("model-configs")]
    public async Task<IEnumerable<ModelConfig>> GetModelConfigs()
    {
        return await repository.GetModelConfigs();
    }

    [HttpGet("models")]
    public Task<List<ModelSummary>> GetModelSummaries(IRepository repository)
    {
        return (from m in repository.GetModels()
                orderby m.Name
                select new ModelSummary()
                {
                    ModelId = m.ModelId,
                    Name = m.Name,
                    Description = m.Description,
                }).ToListAsync();
    }

    [HttpGet("models/{id}")]
    public async Task<ActionResult<Model>> GetModel(IRepository repository, Guid id)
    {
        return await repository.GetModel(id);
    }

    [HttpPost("models")]
    public async Task UpsertModel(IRepository repository, [FromBody] Model model)
    {
        await repository.UpsertModel(model);
    }

    [HttpDelete("models/{id}")]
    public async Task DeleteModel(IRepository repository, Guid id)
    {
        await repository.DeleteModel(id);
    }
}
