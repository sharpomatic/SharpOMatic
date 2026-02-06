namespace SharpOMatic.Engine.Entities.Definitions;

public class NodeEntityConverter : JsonConverter<NodeEntity>
{
    private static readonly Dictionary<NodeType, Type> _nodeEntities;

    static NodeEntityConverter()
    {
        _nodeEntities = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(NodeEntity).IsAssignableFrom(t) && !t.IsAbstract && t.GetCustomAttribute<NodeEntityAttribute>() != null)
            .ToDictionary(t => t.GetCustomAttribute<NodeEntityAttribute>()!.NodeType, t => t);
    }

    public override bool CanConvert(Type typeToConvert) => typeof(NodeEntity).IsAssignableFrom(typeToConvert);

    public override NodeEntity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Utf8JsonReader readerClone = reader;

        if (readerClone.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        NodeType nodeType = default;
        bool typeFound = false;

        while (readerClone.Read())
        {
            if (readerClone.TokenType == JsonTokenType.PropertyName)
            {
                string? propertyName = readerClone.GetString();
                readerClone.Read(); // Move to the value
                if (propertyName?.Equals("NodeType", StringComparison.OrdinalIgnoreCase) == true)
                {
                    nodeType = (NodeType)readerClone.GetInt32();
                    typeFound = true;
                    break;
                }
            }
        }

        if (!typeFound)
            throw new JsonException("Could not find required 'Type' discriminator property.");

        // Cannot use incoming options, overwise called Deserialize will just call this again
        var innerOptions = new JsonSerializerOptions(options);
        innerOptions.Converters.Remove(this);

        if (_nodeEntities.TryGetValue(nodeType, out var entityType))
        {
            return (NodeEntity)JsonSerializer.Deserialize(ref reader, entityType, innerOptions)!;
        }

        throw new NotSupportedException($"NodeType '{nodeType}' is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, NodeEntity value, JsonSerializerOptions options)
    {
        // Cannot use incoming options, overwise called Deserialize will just call this again
        var innerOptions = new JsonSerializerOptions(options);
        innerOptions.Converters.Remove(this);

        JsonSerializer.Serialize(writer, value, value.GetType(), innerOptions);
    }
}
