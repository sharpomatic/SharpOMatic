namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingController : ControllerBase
{
    [HttpGet]
    public async Task<List<Setting>> GetSettings(IRepositoryService repositoryService)
    {
        return await repositoryService.GetSettings();
    }

    [HttpPost]
    public async Task UpsertSetting(
        IRepositoryService repositoryService,
        [FromBody] Setting setting
    )
    {
        await repositoryService.UpsertSetting(setting);
    }
}
