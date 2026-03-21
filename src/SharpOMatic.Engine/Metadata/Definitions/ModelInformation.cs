namespace SharpOMatic.Engine.Metadata.Definitions;

public class ModelInformation
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required FieldDescriptorType Type { get; set; }
    public object? Value { get; set; }
}
