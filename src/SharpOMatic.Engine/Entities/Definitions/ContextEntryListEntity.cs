namespace SharpOMatic.Engine.Entities.Definitions;

public class ContextEntryListEntity : Entity
{
    public required ContextEntryEntity[] Entries { get; set; }
}
