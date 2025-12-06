
namespace SharpOMatic.Engine.Connections;

public class ConnectionConfig
{
    public required string ConfigId { get; set; }
    public required string DisplayName { get; set; }
    public required string Description { get; set; }
    public required List<AuthenticationModeConfig> AuthModes { get; set; }
}