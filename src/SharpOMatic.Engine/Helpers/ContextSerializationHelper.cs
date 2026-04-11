namespace SharpOMatic.Engine.Helpers;

public static class ContextSerializationHelper
{
    private const string SerializationErrorPrefix = "Context contains a value that cannot be serialized:";
    private const string UnsupportedTypePrefix = "Unsupported value type:";

    public static string SerializeForPersistence(ContextObject context, IEnumerable<JsonConverter> jsonConverters)
    {
        try
        {
            return context.Serialize(jsonConverters);
        }
        catch (Exception ex)
        {
            throw new SharpOMaticException(BuildSerializationErrorMessage(ex));
        }
    }

    public static bool IsSerializationFailure(Exception ex)
    {
        return TryBuildSerializationErrorMessage(ex, out _);
    }

    public static string BuildSerializationErrorMessage(Exception ex)
    {
        return TryBuildSerializationErrorMessage(ex, out var message)
            ? message
            : $"{SerializationErrorPrefix} {ex.Message}";
    }

    private static bool TryBuildSerializationErrorMessage(Exception ex, out string message)
    {
        foreach (var current in EnumerateExceptions(ex))
        {
            if (current is SharpOMaticException sharpException)
            {
                if (sharpException.Message.StartsWith(SerializationErrorPrefix, StringComparison.Ordinal))
                {
                    message = sharpException.Message;
                    return true;
                }

                if (sharpException.Message.StartsWith(UnsupportedTypePrefix, StringComparison.Ordinal))
                {
                    message = $"{SerializationErrorPrefix} {sharpException.Message[UnsupportedTypePrefix.Length..].Trim()}";
                    return true;
                }
            }

            if (current is NotSupportedException or JsonException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    message = $"{SerializationErrorPrefix} {current.Message}";
                    return true;
                }
            }
        }

        message = string.Empty;
        return false;
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
            yield return current;
    }
}
