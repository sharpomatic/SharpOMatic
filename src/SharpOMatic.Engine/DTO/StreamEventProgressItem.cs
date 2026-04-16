namespace SharpOMatic.Engine.DTO;

public sealed class StreamEventProgressItem
{
    public required StreamEvent Event { get; init; }
    public bool Silent { get; init; }
}
