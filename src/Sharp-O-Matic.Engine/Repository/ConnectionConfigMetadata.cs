namespace SharpOMatic.Engine.Repository;

public class ConnectionConfigMetadata
{
    [Key]
    public required string ConfigId { get; set; }
    public required string Config { get; set; }
}
