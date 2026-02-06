namespace SharpOMatic.DemoServer;

public sealed class ClassExampleConverter : JsonConverter<ClassExample>
{
    public override ClassExample? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonSerializer.Deserialize<ClassExample>(ref reader, Clean(options));

    public override void Write(Utf8JsonWriter writer, ClassExample value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, Clean(options));

    private JsonSerializerOptions Clean(JsonSerializerOptions options)
    {
        var inner = new JsonSerializerOptions(options);
        inner.Converters.Remove(this);
        return inner;
    }
}
