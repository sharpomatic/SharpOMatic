namespace SharpOMatic.Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Index() {
        return Ok();
    }
}