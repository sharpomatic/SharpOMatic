namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonacoController(ISharpOMaticCode code): ControllerBase
{
    [HttpPost("codecheck")]
    public Task<List<CodeCheckResultModel>> CodeCheck([FromBody] CodeCheckRequestModel request)
    {
        return code.CodeCheck(request);
    }
}
