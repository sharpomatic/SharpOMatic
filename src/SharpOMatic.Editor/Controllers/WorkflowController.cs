namespace SharpOMatic.Editor.Controllers;

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
    public Task<List<WorkflowEditSummary>> GetWorkflowEditSummaries(IRepositoryService repository)
    {
        return repository.GetWorkflowEditSummaries();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowEntity>> GetWorkflow(IRepositoryService repository, Guid id)
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


    [HttpPost("run/{id}")]
    public async Task<ActionResult<Guid>> Run(IEngineService engineService, Guid workflowId)
    {
        // Parse the incoming ContextEntryListEntity data
        using var reader = new StreamReader(Request.Body);
        var contextEntryListEntity = JsonSerializer.Deserialize<ContextEntryListEntity>(await reader.ReadToEndAsync(), _options);

        var runId = await engineService.CreateWorkflowRun(workflowId);
        await engineService.StartWorkflowRunAndNotify(runId, inputEntries: contextEntryListEntity);
        return runId;
    }
}
