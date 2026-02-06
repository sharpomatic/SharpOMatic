namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.End)]
public class EndNodeEntity : NodeEntity
{
    public required bool ApplyMappings { get; set; }
    public required ContextEntryListEntity Mappings { get; set; }
}
