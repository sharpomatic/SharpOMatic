namespace SharpOMatic.Engine.Contexts;

public class ContextObjectConverter : JsonConverter<ContextObject>
{
    public override ContextObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new SharpOMaticException("Expected StartObject for ContextObject.");

        var obj = new ContextObject();

        reader.Read(); // first property or EndObject
        while (reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name in ContextObject.");

            string key = reader.GetString()!;
            reader.Read(); // move to typed value
            obj[key] = ContextTypedValueConverter.ReadTypedValue(ref reader, options);
            reader.Read(); // next property or EndObject
        }

        return obj;
    }

    public override void Write(Utf8JsonWriter writer, ContextObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kv in value.Snapshot())
        {
            writer.WritePropertyName(kv.Key);
            ContextTypedValueConverter.WriteTypedValue(writer, kv.Value, options);
        }

        writer.WriteEndObject();
    }
}
