namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.FrontendToolCall)]
public sealed class FrontendToolCallNodeEntity : NodeEntity
{
    public required string FunctionName { get; set; }
    public required FrontendToolCallArgumentsMode ArgumentsMode { get; set; }
    public required string ArgumentsPath { get; set; }
    public required string ArgumentsJson { get; set; }
    public required string ResultOutputPath { get; set; }
    public required FrontendToolCallChatPersistenceMode ChatPersistenceMode { get; set; }
    public required bool HideFromReplyAfterHandled { get; set; }
}
