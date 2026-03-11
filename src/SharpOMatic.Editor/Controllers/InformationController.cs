namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InformationController : ControllerBase
{
    [HttpGet("forrun/{id}")]
    public async Task<IEnumerable<Information>> GetRunInformations(IRepositoryService repository, Guid id)
    {
        return await repository.GetRunInformations(id);
    }
}
