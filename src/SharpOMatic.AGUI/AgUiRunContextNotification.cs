namespace SharpOMatic.AGUI;

public sealed class AgUiRunContextNotification
{
    public required AgUiRunRequest Request { get; init; }
    public required IReadOnlyDictionary<string, string[]> Headers { get; init; }
    public required string ThreadId { get; init; }
    public required Guid WorkflowId { get; init; }
    public required bool IsConversationEnabled { get; init; }
    public required ContextObject Context { get; init; }
    public required ContextObject Agent { get; init; }
}
