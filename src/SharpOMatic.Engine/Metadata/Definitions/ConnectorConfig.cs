namespace SharpOMatic.Engine.Metadata.Definitions;

public class ConnectorConfig
{
    public required string ConfigId { get; set; }
    public required string DisplayName { get; set; }
    public required string Description { get; set; }
    public required List<AuthenticationModeConfig> AuthModes { get; set; }
}