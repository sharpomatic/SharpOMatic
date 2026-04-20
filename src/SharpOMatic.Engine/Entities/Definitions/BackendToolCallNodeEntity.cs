namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.BackendToolCall)]
public sealed class BackendToolCallNodeEntity : NodeEntity
{
    public required string FunctionName { get; set; }
    public required ToolCallDataMode ArgumentsMode { get; set; }
    public required string ArgumentsPath { get; set; }
    public required string ArgumentsJson { get; set; }
    public required ToolCallDataMode ResultMode { get; set; }
    public required string ResultPath { get; set; }
    public required string ResultJson { get; set; }
    public required ToolCallChatPersistenceMode ChatPersistenceMode { get; set; }
}
