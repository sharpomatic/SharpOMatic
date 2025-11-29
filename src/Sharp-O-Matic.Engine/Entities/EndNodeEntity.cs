namespace SharpOMatic.Engine.Entities;

public class EndNodeEntity : NodeEntity
{
    public required bool ApplyMappings { get; set; }
    public required ContextEntryListEntity Mappings { get; set; }
}

