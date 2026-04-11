namespace SharpOMatic.Engine.DTO;

public sealed class StreamEventWrite
{
    public required StreamEventKind EventKind { get; init; }
    public string? MessageId { get; init; }
    public StreamMessageRole? MessageRole { get; init; }
    public string? TextDelta { get; init; }
    public string? Metadata { get; init; }
}
