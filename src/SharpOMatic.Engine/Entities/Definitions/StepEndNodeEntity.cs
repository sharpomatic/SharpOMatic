namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.StepEnd)]
public sealed class StepEndNodeEntity : NodeEntity
{
    public required string StepName { get; set; }
}
