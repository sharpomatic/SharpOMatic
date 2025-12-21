namespace SharpOMatic.Engine.Services;

public class SchemaTypeService(IEnumerable<Type> types) : ISchemaTypeService
{
    private readonly Dictionary<string, Type> _types = types.ToDictionary(t => t.Name, t => t);

    public IEnumerable<string> GetTypeNames()
    {
        return _types.Keys;
    }

    public string GetSchema(string typeName)
    {
        if (!_types.TryGetValue(typeName, out var type))
        {
            throw new ArgumentException($"Type '{typeName}' not found.", nameof(typeName));
        }

        var schema = AIJsonUtilities.CreateJsonSchema(type);
        return JsonSerializer.Serialize(schema);
    }
}
