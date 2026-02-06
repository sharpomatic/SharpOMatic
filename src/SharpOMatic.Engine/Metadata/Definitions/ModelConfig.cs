namespace SharpOMatic.Engine.Metadata.Definitions;

public class ModelConfig
{
    public required int Version { get; set; }
    public required string ConfigId { get; set; }
    public required string DisplayName { get; set; }
    public required string Description { get; set; }
    public required string ConnectorConfigId { get; set; }
    public required bool IsCustom { get; set; }
    public required List<ModelCapability> Capabilities { get; set; }
    public required List<FieldDescriptor> ParameterFields { get; set; }
}
