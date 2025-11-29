namespace SharpOMatic.Engine.Entities;

public class ConnectionEntity : Entity
{
    public required Guid From { get; set; }
    public required Guid To { get; set; }
}

