namespace SharpOMatic.Engine.Contexts;

public static class ContextTypeRegistry
{
    // Whitelisted scalar element types
    private static readonly (Type Type, string Name)[] Scalars =
    {
        // c# scalars
        (typeof(bool),   "Boolean"),
        (typeof(byte),   "Byte"),
        (typeof(sbyte),  "SByte"),
        (typeof(short),  "Int16"),
        (typeof(ushort), "UInt16"),
        (typeof(int),    "Int32"),
        (typeof(uint),   "UInt32"),
        (typeof(long),   "Int64"),
        (typeof(ulong),  "UInt64"),
        (typeof(float),  "Single"),
        (typeof(double), "Double"),
        (typeof(decimal),"Decimal"),
        (typeof(char),   "Char"),
        (typeof(string), "String"),

        // .net scalars
        (typeof(DateTime),       "DateTime"),
        (typeof(DateTimeOffset), "DateTimeOffset"),
        (typeof(TimeSpan),       "TimeSpan"),
        (typeof(DateOnly),       "DateOnly"),
        (typeof(TimeOnly),       "TimeOnly"),
        (typeof(Guid),           "Guid"),
        (typeof(Uri),            "Uri"),
    };

    private static readonly Dictionary<Type, string> TypeToName = Scalars.ToDictionary(x => x.Type, x => x.Name);
    private static readonly Dictionary<string, Type> NameToType = Scalars.ToDictionary(x => x.Name, x => x.Type, StringComparer.Ordinal);

    public static bool IsSupportedScalar(Type t) => TypeToName.ContainsKey(t);

    public static bool IsSupportedArray(Type t)
    {
        if (!t.IsArray) 
            return false;

        // Jagged only; each level must be rank-1
        var cur = t;
        while (cur.IsArray)
        {
            if (cur.GetArrayRank() != 1) 
                return false;

            cur = cur.GetElementType()!;
        }

        // cur is terminal element type; allow Nullable<T> where T is a supported scalar
        if (IsNullableValueType(cur, out var underlying))
            return IsSupportedScalar(underlying!);

        return IsSupportedScalar(cur); // reference types / non-nullable value types
    }

    public static string GetTokenFor(Type t)
    {
        // Scalars (note: non-null scalars are never Nullable<T> at runtime after boxing)
        if (IsSupportedScalar(t)) 
            return TypeToName[t];

        if (!t.IsArray)
            throw new SharpOMaticException($"Unsupported type: {t.FullName}");

        // Count jagged depth (rank==1 per level), reject rectangular
        int depth = 0;
        var cur = t;
        while (cur.IsArray)
        {
            if (cur.GetArrayRank() != 1)
                throw new SharpOMaticException("Rectangular arrays are not supported.");

            depth++;
            cur = cur.GetElementType()!;
        }

        // Detect nullable element (value types only)
        bool elemIsNullable = IsNullableValueType(cur, out var underlying);
        var elemScalar = elemIsNullable ? underlying! : cur;

        if (!IsSupportedScalar(elemScalar))
            throw new SharpOMaticException($"Unsupported array element type: {elemScalar.FullName}");

        var baseName = TypeToName[elemScalar];

        // We only emit '?' for nullable **value** element types (e.g., Int32?[])
        var nullableSuffix = elemIsNullable ? "?" : string.Empty;

        return baseName + nullableSuffix + string.Concat(Enumerable.Repeat("[]", depth));
    }

    public static bool TryResolve(string token, out Type? resolved)
    {
        resolved = null;

        // Split base (with optional '?') + trailing []...[] (jagged only)
        int bracketIdx = token.IndexOf('[');
        string head = bracketIdx >= 0 ? token[..bracketIdx] : token;
        string tails = bracketIdx >= 0 ? token[bracketIdx..] : string.Empty;

        // Head may be "Int32" or "Int32?" (we also accept "Uri?" for typed-null parsing)
        bool nullableHead = head.EndsWith("?", StringComparison.Ordinal);
        string baseName = nullableHead ? head[..^1] : head;

        if (!NameToType.TryGetValue(baseName, out var baseType))
            return false;

        // Build element type: Nullable<T> only for value types
        Type elemType = baseType;
        if (nullableHead && baseType.IsValueType && Nullable.GetUnderlyingType(baseType) is null)
            elemType = typeof(Nullable<>).MakeGenericType(baseType);

        // Parse only sequences of "[]"
        int p = 0;
        int depth = 0;
        while (p < tails.Length)
        {
            if (p + 1 >= tails.Length || tails[p] != '[' || tails[p + 1] != ']')
                return false; // anything else (e.g., commas) => reject

            depth++;
            p += 2;
        }

        Type t = elemType;
        for (int i = 0; i < depth; i++) t = t.MakeArrayType();

        // Validate final array shape/type
        if (depth > 0 && !IsSupportedArray(t)) 
            return false;

        if (depth == 0 && !(IsSupportedScalar(t) || (IsNullableValueType(t, out var u) && IsSupportedScalar(u!))))
            return false;

        resolved = t;
        return true;
    }

    private static bool IsNullableValueType(Type t, out Type? underlying)
    {
        underlying = Nullable.GetUnderlyingType(t);
        return underlying != null;
    }
}
