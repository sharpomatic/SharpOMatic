namespace SharpOMatic.Engine.Contexts;

public class ContextListConverter : JsonConverter<ContextList>
{
    public override ContextList? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new SharpOMaticException("Expected StartArray for ContextList.");

        var list = new ContextList();

        reader.Read(); // first element or EndArray
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(ContextTypedValueConverter.ReadTypedValue(ref reader, options));
            reader.Read(); // next element or EndArray
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, ContextList value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value)
            ContextTypedValueConverter.WriteTypedValue(writer, item, options);

        writer.WriteEndArray();
    }
}
