namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TraceController : ControllerBase
{
    [HttpGet("forrun/{id}")]
    public async Task<IEnumerable<Trace>> GetRunTraces(ISharpOMaticRepository repository, Guid id)
    {
        return await (from t in repository.GetRunTraces(id)
                      orderby t.Created
                      select t).ToListAsync();
    }
}
