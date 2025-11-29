namespace SharpOMatic.Engine.Entities;

public abstract class NodeEntity : Entity
{
    public required NodeType NodeType { get; set; }
    public required string Title { get; set; }
    public required float Top { get; set; }
    public required float Left { get; set; }
    public required float Width { get; set; }
    public required float Height { get; set; }
    public required ConnectorEntity[] Inputs { get; set; }
    public required ConnectorEntity[] Outputs { get; set; }
}

