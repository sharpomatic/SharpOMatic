namespace SharpOMatic.Engine.Entities.Definitions;

public abstract class Entity
{
    public required int Version { get; set; }
    public required Guid Id { get; set; }
}
