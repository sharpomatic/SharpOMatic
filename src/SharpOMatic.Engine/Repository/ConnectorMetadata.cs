namespace SharpOMatic.Engine.Repository;

public class ConnectorMetadata
{
    [Key]
    public required Guid ConnectorId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Config { get; set; }
}
