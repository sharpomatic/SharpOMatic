namespace SharpOMatic.Engine.Entities;

public class EditNodeEntity : NodeEntity
{
    public required ContextEntryListEntity Edits { get; set; }
}

