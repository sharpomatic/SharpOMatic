namespace SharpOMatic.Engine.Exceptions;

public sealed class ModelResponseErrorException : SharpOMaticException
{
    [SetsRequiredMembers]
    public ModelResponseErrorException(string? errorCode, string? message, string? details)
        : base(BuildMessage(errorCode, message, details))
    {
        ErrorCode = errorCode;
        Details = details;
    }

    public string? ErrorCode { get; }

    public string? Details { get; }

    public int? StatusCode => int.TryParse(ErrorCode, out var statusCode) ? statusCode : null;

    private static string BuildMessage(string? errorCode, string? message, string? details)
    {
        StringBuilder sb = new("Model returned an error response.");

        if (!string.IsNullOrWhiteSpace(errorCode))
            sb.Append(" [").Append(errorCode.Trim()).Append(']');

        sb.Append(' ').Append(string.IsNullOrWhiteSpace(message) ? "No error message was provided." : message.Trim());

        if (!string.IsNullOrWhiteSpace(details))
            sb.Append(" (").Append(details.Trim()).Append(')');

        return sb.ToString();
    }
}
