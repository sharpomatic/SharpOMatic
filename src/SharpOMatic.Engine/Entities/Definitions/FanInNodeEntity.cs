namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.FanIn)]
public class FanInNodeEntity : NodeEntity
{
    public required string MergePath { get; set; }
}
