namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.ModelCall)]
public class ModelCallNodeEntity : NodeEntity
{
    public required Guid? ModelId { get; set; }
    public required string Instructions { get; set; }
    public required string Prompt { get; set; }
    public required string TextOutputPath { get; set; }
    public required string ImageOutputPath { get; set; }
}
