namespace SharpOMatic.Engine.Contexts;

public static class ContextTypedValueConverter
{
    private const string TypeProp = "$type";
    private const string ValueProp = "value";

    public static void WriteTypedValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value is null)
        {
            writer.WriteString(TypeProp, "Null");
            writer.WriteNull(ValueProp);
            writer.WriteEndObject();
            return;
        }

        var t = value.GetType();

        if (t == typeof(ContextList))
        {
            writer.WriteString(TypeProp, "ContextList");
            writer.WritePropertyName(ValueProp);
            JsonSerializer.Serialize(writer, (ContextList)value, options);
        }
        else if (t == typeof(ContextObject))
        {
            writer.WriteString(TypeProp, "ContextObject");
            writer.WritePropertyName(ValueProp);
            JsonSerializer.Serialize(writer, (ContextObject)value, options);
        }
        else if (ContextTypeRegistry.IsSupportedScalar(t))
        {
            writer.WriteString(TypeProp, ContextTypeRegistry.GetTokenFor(t));
            writer.WritePropertyName(ValueProp);
            JsonSerializer.Serialize(writer, value, t, options);
        }
        else if (t.IsArray && ContextTypeRegistry.IsSupportedArray(t))
        {
            writer.WriteString(TypeProp, ContextTypeRegistry.GetTokenFor(t));
            writer.WritePropertyName(ValueProp);
            JsonSerializer.Serialize(writer, value, t, options);
        }
        else if (TryGetExternalTokenFor(t, options, out var externalToken))
        {
            writer.WriteString(TypeProp, externalToken);
            writer.WritePropertyName(ValueProp);
            JsonSerializer.Serialize(writer, value, t, options);
        }
        else
        {
            throw new SharpOMaticException($"Unsupported value type: {t.FullName}");
        }

        writer.WriteEndObject();
    }

    public static object? ReadTypedValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new SharpOMaticException("Expected StartObject for typed value.");

        string? typeToken = null;
        object? result = null;
        bool haveValue = false;

        reader.Read();
        while (reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new SharpOMaticException("Expected property name in typed value.");

            string prop = reader.GetString()!;
            reader.Read();

            if (prop == TypeProp)
            {
                typeToken = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new SharpOMaticException("Expected string for $type.");
            }
            else if (prop == ValueProp)
            {
                if (typeToken is null)
                    throw new SharpOMaticException("Encountered 'value' before '$type'.");

                result = ReadByTypeToken(ref reader, typeToken, options);
                haveValue = true;
            }
            else
            {
                throw new SharpOMaticException($"Unknown property '{prop}' in typed value.");
            }

            reader.Read();
        }

        if (typeToken is null || !haveValue)
            throw new SharpOMaticException("Typed value must contain both '$type' and 'value'.");

        return result;
    }

    private static object? ReadByTypeToken(ref Utf8JsonReader reader, string typeToken, JsonSerializerOptions options)
    {
        // Special containers
        if (typeToken == "ContextList")
            return JsonSerializer.Deserialize<ContextList>(ref reader, options) ?? throw new SharpOMaticException("ContextList null.");

        if (typeToken == "ContextObject")
            return JsonSerializer.Deserialize<ContextObject>(ref reader, options) ?? throw new SharpOMaticException("ContextObject null.");

        if (typeToken == "Null")
            return reader.TokenType == JsonTokenType.Null ? null : throw new SharpOMaticException("Null expected.");

        if (ContextTypeRegistry.TryResolve(typeToken, out var builtIn) && builtIn is not null)
        {
            // Handle typed-null for scalars/reference types
            if (reader.TokenType == JsonTokenType.Null)
            {
                if (builtIn.IsArray)
                    throw new SharpOMaticException($"Null for '{typeToken}' not allowed.");

                if (Nullable.GetUnderlyingType(builtIn) != null)
                    return null;

                if (!builtIn.IsValueType)
                    return null;

                throw new SharpOMaticException($"Null for '{typeToken}' not allowed.");
            }

            return JsonSerializer.Deserialize(ref reader, builtIn, options) ?? throw new JsonException($"Null for '{typeToken}' not allowed.");
        }

        // External types (scalar or jagged arrays)
        if (TryResolveExternalType(typeToken, options, out var extType))
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                if (extType.IsArray)
                    throw new SharpOMaticException($"Null for '{typeToken}' not allowed.");

                if (Nullable.GetUnderlyingType(extType) != null)
                    return null;

                if (!extType.IsValueType)
                    return null;

                throw new SharpOMaticException($"Null for '{typeToken}' not allowed.");
            }
            return JsonSerializer.Deserialize(ref reader, extType, options) ?? throw new JsonException($"Null for '{typeToken}' not allowed.");
        }

        throw new SharpOMaticException($"Unsupported $type '{typeToken}'.");
    }

    private static bool TryGetExternalTokenFor(Type runtimeType, JsonSerializerOptions options, out string token)
    {
        token = default!;
        var cfg = ExternalTypesConverter.From(options);
        if (cfg is null)
            return false;

        // Jagged arrays only. Count [] depth and find element type.
        int depth = 0;
        var t = runtimeType;
        while (t.IsArray)
        {
            if (t.GetArrayRank() != 1)
                return false;

            depth++;
            t = t.GetElementType()!;
        }

        // Handle nullable value-type element (for arrays); scalars never appear as Nullable<T> after boxing
        bool elemNullable = false;
        var elem = t;
        var underlying = Nullable.GetUnderlyingType(elem);
        if (underlying != null)
        {
            elemNullable = true;
            elem = underlying;
        }

        // Look up base token
        if (!cfg.TypeToToken.TryGetValue(elem, out var baseToken))
            return false;

        var head = elem.IsValueType && elemNullable ? baseToken + "?" : baseToken;
        token = head + string.Concat(Enumerable.Repeat("[]", depth));
        return true;
    }

    private static bool TryResolveExternalType(string token, JsonSerializerOptions options, out Type resolved)
    {
        resolved = default!;
        var cfg = ExternalTypesConverter.From(options);
        if (cfg is null)
            return false;

        // Split head + tails (jagged only: "[]...[]")
        int idx = token.IndexOf('[');
        string head = idx >= 0 ? token[..idx] : token;
        string tails = idx >= 0 ? token[idx..] : string.Empty;

        bool nullableHead = head.EndsWith("?", StringComparison.Ordinal);
        string baseName = nullableHead ? head[..^1] : head;

        if (!cfg.TokenToType.TryGetValue(baseName, out var baseType))
            return false;

        // Parse []…[] only (no commas)
        int p = 0,
            depth = 0;
        while (p < tails.Length)
        {
            if (p + 1 >= tails.Length || tails[p] != '[' || tails[p + 1] != ']')
                return false;

            depth++;
            p += 2;
        }

        // Compose element type: Nullable<T> only valid for value types
        Type elemType = baseType;
        if (nullableHead && baseType.IsValueType && Nullable.GetUnderlyingType(baseType) is null)
            elemType = typeof(Nullable<>).MakeGenericType(baseType);

        // Build jagged array if needed
        Type t = elemType;
        for (int i = 0; i < depth; i++)
            t = t.MakeArrayType();

        resolved = t;
        return true;
    }
}
