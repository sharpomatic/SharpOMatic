namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.EventTemplate)]
public sealed class EventTemplateNodeEntity : NodeEntity
{
    public required string Template { get; set; }
    public required EventTemplateOutputMode OutputMode { get; set; }
    public required StreamMessageRole TextRole { get; set; }
    public bool Silent { get; set; }
}
