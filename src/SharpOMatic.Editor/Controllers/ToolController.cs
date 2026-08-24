namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ToolController : ControllerBase
{
    [HttpGet]
    public IEnumerable<ToolMethodDescriptor> GetToolMethods(IToolMethodRegistry toolMethodRegistry)
    {
        return toolMethodRegistry.GetToolMethods();
    }
}
