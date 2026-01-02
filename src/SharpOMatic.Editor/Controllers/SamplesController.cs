namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SamplesController : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<string>> GetWorkflowNames(ISamplesService samplesService)
    {
        return samplesService.GetWorkflowNames();
    }

    [HttpPost("{sampleName}")]
    public async Task<ActionResult<Guid>> CreateWorkflow(ISamplesService samplesService, [FromRoute] string sampleName)
    {
        var workflowId = await samplesService.CreateWorkflow(sampleName);
        return workflowId;
    }
}
