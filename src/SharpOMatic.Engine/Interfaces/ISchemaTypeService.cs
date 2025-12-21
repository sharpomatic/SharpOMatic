namespace SharpOMatic.Engine.Interfaces;

public interface ISchemaTypeService
{
    IEnumerable<string> GetTypeNames();
    string GetSchema(string typeName);
}
