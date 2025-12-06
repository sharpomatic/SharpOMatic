namespace SharpOMatic.Engine.Repository;

public class ConnectionMetadata
{
    [Key]
    public required string Id { get; set; }
    public required string Config { get; set; }
}
