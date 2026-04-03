namespace SharpOMatic.Engine.DTO;

public sealed class ConversationGosubFrame
{
    public required Guid ChildWorkflowId { get; init; }
    public required string ParentContextJson { get; init; }
    public Guid? ReturnNodeId { get; init; }
    public required Guid ParentTraceId { get; init; }
    public required bool ApplyOutputMappings { get; init; }
    public required string OutputMappingsJson { get; init; }
}
