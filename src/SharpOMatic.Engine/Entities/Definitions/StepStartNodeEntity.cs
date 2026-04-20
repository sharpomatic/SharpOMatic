namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.StepStart)]
public sealed class StepStartNodeEntity : NodeEntity
{
    public required string StepName { get; set; }
}
