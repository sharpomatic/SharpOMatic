using System.Diagnostics;

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
    public async Task<Run?> GetLatestRunForWorkflow(ISharpOMaticRepository repository, Guid id)
    {
        var run = await (from r in repository.GetWorkflowRuns(id)
                         orderby r.Created descending
                         select r).FirstOrDefaultAsync();

        if ((run is not null) && (run.InputEntries is not null))
        {
            // Covert to camalCase which is used by the front end
            var x = JsonSerializer.Deserialize<ContextEntryListEntity>(run.InputEntries);
            run.InputEntries = JsonSerializer.Serialize(x, _options);
        }

        return run;
    }
}
