namespace SharpOMatic.Engine.Entities;

public class ContextEntryEntity : Entity
{
    public required ContextEntryPurpose Purpose { get; set; }
    public required string InputPath { get; set; }
    public required string OutputPath { get; set; }
    public required bool Optional { get; set; }
    public required ContextEntryType EntryType { get; set; }
    public required string EntryValue { get; set; }
}
