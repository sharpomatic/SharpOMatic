namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.FanOut)]
public class FanOutNodeEntity : NodeEntity
{
    public required string[] Names { get; set; }
}
