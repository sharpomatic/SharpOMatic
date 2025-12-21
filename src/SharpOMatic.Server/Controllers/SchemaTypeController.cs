namespace SharpOMatic.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchemaTypeController : ControllerBase
{
    [HttpGet]
    public IEnumerable<string> GetSchemaTypeNames(ISchemaTypeService schematypeService)
    {
        return schematypeService.GetTypeNames();
    }

    [HttpGet("{typeName}")]
    public string GetSchemaTypeNames(ISchemaTypeService schematypeService, string typeName)
    {
        return schematypeService.GetSchema(typeName);
    }
}
