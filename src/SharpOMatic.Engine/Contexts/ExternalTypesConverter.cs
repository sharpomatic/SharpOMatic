namespace SharpOMatic.Engine.Contexts;

public class ExternalTypesConverter(IDictionary<Type, string> typeToToken, IDictionary<string, Type> tokenToType) : JsonConverter<object>
{
    public IReadOnlyDictionary<Type, string> TypeToToken { get; } = new Dictionary<Type, string>(typeToToken);
    public IReadOnlyDictionary<string, Type> TokenToType { get; } = new Dictionary<string, Type>(tokenToType);

    public override bool CanConvert(Type typeToConvert) => false;

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) => throw new NotSupportedException();

    public static ExternalTypesConverter? From(JsonSerializerOptions options) => options.Converters.OfType<ExternalTypesConverter>().FirstOrDefault();
}
