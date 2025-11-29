namespace SharpOMatic.Engine.Entities;

public class WorkflowEntity : Entity
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required NodeEntity[] Nodes { get; set; }
    public required ConnectionEntity[] Connections { get; set; }
}

