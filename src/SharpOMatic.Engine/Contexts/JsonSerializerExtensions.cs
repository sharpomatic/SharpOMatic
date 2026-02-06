namespace SharpOMatic.Engine.Contexts;

public static class JsonSerializerExtensions
{
    public static JsonSerializerOptions BuildOptions(this JsonSerializerOptions baseOptions, IEnumerable<JsonConverter>? extraConverters = null)
    {
        var o = new JsonSerializerOptions(baseOptions);

        ApplyAIDefaultOptions(o);

        var typeToToken = new Dictionary<Type, string>();
        var tokenToType = new Dictionary<string, Type>();

        RegisterExternalType(typeToToken, tokenToType, typeof(ChatMessage));
        RegisterExternalType(typeToToken, tokenToType, typeof(AssetRef));

        if (extraConverters != null)
        {
            foreach (var c in extraConverters)
            {
                Type converterType = c.GetType();
                Type? targetType = converterType.BaseType?.GetGenericArguments().FirstOrDefault();
                if (targetType is not null)
                {
                    RegisterExternalType(typeToToken, tokenToType, targetType);
                    o.Converters.Add(c);
                }
            }
        }

        if (typeToToken.Count > 0)
            o.Converters.Add(new ExternalTypesConverter(typeToToken, tokenToType));

        o.Converters.Add(new ContextListConverter());
        o.Converters.Add(new ContextObjectConverter());

        return o;
    }

    private static void RegisterExternalType(Dictionary<Type, string> typeToToken, Dictionary<string, Type> tokenToType, Type type)
    {
        if (typeToToken.ContainsKey(type))
            return;

        var token = type.Name;
        typeToToken[type] = token;
        if (!tokenToType.ContainsKey(token))
            tokenToType[token] = type;
    }

    private static void ApplyAIDefaultOptions(JsonSerializerOptions options)
    {
        var defaults = AIJsonUtilities.DefaultOptions;

        if (defaults.TypeInfoResolver is not null)
        {
            if (options.TypeInfoResolver is null)
                options.TypeInfoResolver = defaults.TypeInfoResolver;
            else if (!ReferenceEquals(options.TypeInfoResolver, defaults.TypeInfoResolver))
                options.TypeInfoResolverChain.Insert(0, defaults.TypeInfoResolver);
        }

        foreach (var converter in defaults.Converters)
            if (!options.Converters.Any(c => c.GetType() == converter.GetType()))
                options.Converters.Add(converter);
    }
}
