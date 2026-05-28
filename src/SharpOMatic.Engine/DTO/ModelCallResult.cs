namespace SharpOMatic.Engine.DTO;

public sealed class ModelCallResult
{
    public required IList<ChatMessage> Chat { get; init; }
    public required IList<ChatMessage> Responses { get; init; }
    public object? ResultValue { get; init; }
    public UsageDetails? Usage { get; init; }
    public string? ProviderModelName { get; init; }

    public static implicit operator ModelCallResult((IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue) result)
    {
        return new ModelCallResult()
        {
            Chat = result.chat,
            Responses = result.responses,
            ResultValue = result.resultValue,
        };
    }
}
