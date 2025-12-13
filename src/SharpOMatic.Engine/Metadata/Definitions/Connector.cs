namespace SharpOMatic.Engine.Metadata.Definitions;

public class Connector : ConnectorSummary
{
    public required string ConfigId { get; set; }
    public required string AuthenticationModeId { get; set; }
    public required Dictionary<string, string?> FieldValues { get; set; }
}
