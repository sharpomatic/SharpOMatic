namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonacoController(ICodeCheck code): ControllerBase
{
    [HttpPost("codecheck")]
    public Task<List<CodeCheckResult>> CodeCheck([FromBody] CodeCheckRequest request)
    {
        return code.CodeCheck(request);
    }
}
