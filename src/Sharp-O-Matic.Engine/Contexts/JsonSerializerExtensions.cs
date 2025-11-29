namespace SharpOMatic.Engine.Contexts;

public static class JsonSerializerExtensions
{
    public static JsonSerializerOptions BuildOptions(this JsonSerializerOptions? baseOptions,
                                                     IEnumerable<JsonConverter>? extraConverters = null)
    {
        var o = baseOptions is null ? new JsonSerializerOptions() : new JsonSerializerOptions(baseOptions)
        {
            Converters = { }
        };

        if (extraConverters != null)
        {
            var typeToToken = new Dictionary<Type, string>();
            var tokenToType = new Dictionary<string, Type>();

            foreach (var c in extraConverters)
            {
                Type converterType = c.GetType();
                Type? targetType = converterType.BaseType?.GetGenericArguments().FirstOrDefault();
                if (targetType is not null)
                {
                    typeToToken.Add(targetType, targetType.Name);
                    tokenToType.Add(targetType.Name, targetType);

                    o.Converters.Add(c);
                }
            }

            o.Converters.Add(new ExternalTypesConverter(typeToToken, tokenToType));
        }

        o.Converters.Add(new ContextListConverter());
        o.Converters.Add(new ContextObjectConverter());

        return o;
    }
}
