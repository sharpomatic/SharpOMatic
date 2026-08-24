namespace SharpOMatic.Engine.Helpers;

/// <summary>
/// Serializes a value to the JSON a model should read, as opposed to the JSON the context is persisted as. Context
/// persistence wraps every value in a <c>{"$type": ..., "value": ...}</c> envelope so it can be read back with its type
/// intact, which is noise in a prompt. This produces plain camelCase JSON with nulls omitted, and is stored as a string
/// so a <c>{{path}}</c> template inserts it verbatim instead of re-serializing it.
/// </summary>
public static class PromptJsonHelper
{
    private static readonly JsonSerializerOptions PromptOptions = new(AIJsonUtilities.DefaultOptions)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize<TValue>(TValue value)
    {
        return NeutralizeTemplateMarkers(JsonSerializer.Serialize(value, PromptOptions));
    }

    /// <summary>
    /// Escapes the opening halves of the two template markers so text carried inside the JSON can never be mistaken for
    /// a template. Without this, a stored message containing <c>{{expected.answer}}</c> would be substituted from the
    /// surrounding context when the prompt is rendered, and a message containing <c>&lt;&lt;name&gt;&gt;</c> would be
    /// treated as an asset reference; an unresolvable path fails the whole model call.
    /// Only the opening halves are escaped, because neither <c>{{</c> nor <c>&lt;&lt;</c> can occur in JSON structure,
    /// while <c>}}</c> occurs in it constantly. The escapes are ordinary JSON string escapes, so a parser still reads
    /// back the original text.
    /// </summary>
    private static string NeutralizeTemplateMarkers(string json)
    {
        return json.Replace("{{", "{\\u007B", StringComparison.Ordinal).Replace("<<", "<\\u003C", StringComparison.Ordinal);
    }
}
