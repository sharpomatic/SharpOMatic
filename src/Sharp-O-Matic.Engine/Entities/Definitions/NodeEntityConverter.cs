using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SharpOMatic.Engine.Entities.Definitions;

public class NodeEntityConverter : JsonConverter<NodeEntity>
{
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

        return nodeType switch
        {
            NodeType.Start => JsonSerializer.Deserialize<StartNodeEntity>(ref reader, innerOptions)!,
            NodeType.End => JsonSerializer.Deserialize<EndNodeEntity>(ref reader, innerOptions)!,
            NodeType.Code => JsonSerializer.Deserialize<CodeNodeEntity>(ref reader, innerOptions)!,
            NodeType.Edit => JsonSerializer.Deserialize<EditNodeEntity>(ref reader, innerOptions)!,
            NodeType.Switch => JsonSerializer.Deserialize<SwitchNodeEntity>(ref reader, innerOptions)!,
            NodeType.FanIn => JsonSerializer.Deserialize<FanInNodeEntity>(ref reader, innerOptions)!,
            NodeType.FanOut => JsonSerializer.Deserialize<FanOutNodeEntity>(ref reader, innerOptions)!,
            NodeType.ModelCall => JsonSerializer.Deserialize<ModelCallNodeEntity>(ref reader, innerOptions)!,
            _ => throw new NotSupportedException($"NodeType '{nodeType}' is not supported.")
        };
    }

    public override void Write(Utf8JsonWriter writer, NodeEntity value, JsonSerializerOptions options)
    {
        // Cannot use incoming options, overwise called Deserialize will just call this again
        var innerOptions = new JsonSerializerOptions(options);
        innerOptions.Converters.Remove(this);

        switch (value)
        {
            case StartNodeEntity start:
                JsonSerializer.Serialize(writer, start, innerOptions);
                break;
            case EndNodeEntity end:
                JsonSerializer.Serialize(writer, end, innerOptions);
                break;
            case CodeNodeEntity code:
                JsonSerializer.Serialize(writer, code, innerOptions);
                break;
            case EditNodeEntity edit:
                JsonSerializer.Serialize(writer, edit, innerOptions);
                break;
            case SwitchNodeEntity switcher:
                JsonSerializer.Serialize(writer, switcher, innerOptions);
                break;
            case FanInNodeEntity fanIn:
                JsonSerializer.Serialize(writer, fanIn, innerOptions);
                break;
            case FanOutNodeEntity fanOut:
                JsonSerializer.Serialize(writer, fanOut, innerOptions);
                break;
            case ModelCallNodeEntity modelCall:
                JsonSerializer.Serialize(writer, modelCall, innerOptions);
                break;
            default:
                throw new NotSupportedException($"Type '{value.GetType()}' is not supported.");
        }
    }
}
