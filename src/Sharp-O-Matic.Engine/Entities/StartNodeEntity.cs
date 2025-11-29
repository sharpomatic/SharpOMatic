namespace SharpOMatic.Engine.Entities;

public class StartNodeEntity : NodeEntity
{
    public required bool ApplyInitialization { get; set; }
    public required ContextEntryListEntity Initializing { get; set; }
}

