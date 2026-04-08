namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamEventController : ControllerBase
{
    [HttpGet("forrun/{id}")]
    public async Task<IEnumerable<StreamEvent>> GetRunStreamEvents(IRepositoryService repository, Guid id)
    {
        return await repository.GetRunStreamEvents(id);
    }
}
