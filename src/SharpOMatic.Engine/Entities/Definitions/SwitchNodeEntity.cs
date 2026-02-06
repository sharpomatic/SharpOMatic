namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.Switch)]
public class SwitchNodeEntity : NodeEntity
{
    public required SwitchEntryEntity[] Switches { get; set; }
}
