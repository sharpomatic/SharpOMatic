namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.Code)]
public class CodeNodeEntity : NodeEntity
{
    public required string Code { get; set; }
}
