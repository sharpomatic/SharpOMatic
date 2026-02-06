namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.Start)]
public class StartNodeEntity : NodeEntity
{
    public required bool ApplyInitialization { get; set; }
    public required ContextEntryListEntity Initializing { get; set; }
}
