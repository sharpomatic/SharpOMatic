namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingController : ControllerBase
{
    [HttpGet]
    public async Task<List<Setting>> GetSettings(IRepository repository)
    {
        return await (from s in repository.GetSettings()
                      orderby s.Name
                      select s).ToListAsync();
    }

    [HttpPost]
    public async Task UpsertSetting(IRepository repository, [FromBody] Setting setting)
    {
        await repository.UpsertSetting(setting);
    }
}
