using SharpOMatic.Engine.Exceptions;
using SharpOMatic.Engine.Helpers;

namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private static readonly JsonSerializerOptions _options = new() 
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new NodeEntityConverter() }
    };

    [HttpGet]
    public Task<List<WorkflowEditSummary>> GetWorkflowEditSummaries(IRepository repository)
    {
        return (from w in repository.GetWorkflows()
                orderby w.Named
                select new WorkflowEditSummary()
                {
                    Id = w.WorkflowId,
                    Name = w.Named,
                    Description = w.Description,
                }).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowEntity>> GetWorkflow(IRepository repository, Guid id)
    {
        return await repository.GetWorkflow(id);
    }

    [HttpPost()]
    public async Task UpsertWorkflow(IRepository repository)
    {
        string requestBody;
        using var reader = new StreamReader(Request.Body);
        requestBody = await reader.ReadToEndAsync();
        var workflow = JsonSerializer.Deserialize<WorkflowEntity>(requestBody, _options);
        await repository.UpsertWorkflow(workflow!);
    }

    [HttpDelete("{id}")]
    public async Task DeleteWorkflow(IRepository repository, Guid id)
    {
        await repository.DeleteWorkflow(id);
    }


    [HttpPost("run/{id}")]
    public async Task<ActionResult<Guid>> Run(IEngine engine, Guid id)
    {
        // Parse the incoming ContextEntryListEntity data
        using var reader = new StreamReader(Request.Body);
        var contextEntryListEntity = JsonSerializer.Deserialize<ContextEntryListEntity>(await reader.ReadToEndAsync(), _options);

        return await engine.RunWorkflow(id, inputEntries: contextEntryListEntity);
    }
}
