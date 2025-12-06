namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetadataController(IRepository repository) : ControllerBase
{
    [HttpGet("connections")]
    public async Task<IEnumerable<ConnectionConfig>> GetConnectionConfigs()
    {
        return await repository.GetConnectionConfigs();
    }
}
