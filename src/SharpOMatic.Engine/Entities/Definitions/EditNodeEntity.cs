namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.Edit)]
public class EditNodeEntity : NodeEntity
{
    public required ContextEntryListEntity Edits { get; set; }
}
