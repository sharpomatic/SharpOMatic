
namespace SharpOMatic.Engine.Connections;

public class FieldDescriptor
{
    public required string Name { get; set; }
    public required string Label { get; set; }
    public required string Description { get; set; }
    public required FieldDescriptorType Type { get; set; }
    public required bool IsRequired { get; set; }
    public required object? DefaultValue { get; set; }
    public required List<string> EnumOptions { get; set; }
}