namespace SharpOMatic.Engine.Metadata.Definitions;

public class Model : ModelSummary
{
    public required string ConfigId { get; set; }
    public required Guid? ConnectorId { get; set; }
    public required List<string> CustomCapabilities { get; set; }
    public required Dictionary<string, string?> ParameterValues { get; set; }
}

