namespace SharpOMatic.Engine.Metadata.Definitions;

public class ConnectorSummary
{
    public required Guid ConnectorId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}
