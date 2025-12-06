namespace SharpOMatic.Engine.Connections;

public class ConnectionSummary
{
    public required Guid ConnectionId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}
