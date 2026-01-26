namespace SharpOMatic.Engine.Repository;

public class EvalColumn
{
    [Key]
    public required Guid EvalColumnId { get; set; }
    public required Guid EvalConfigId { get; set; }
    public required string Name { get; set; }
    public required int Order { get; set; }
    public required ContextEntryType EntryType { get; set; }
    public required bool Optional { get; set; }
    public required string? InputPath { get; set; }
}
