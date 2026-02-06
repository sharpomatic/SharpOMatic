namespace SharpOMatic.Engine.Metadata.Definitions;

public class AuthenticationModeConfig
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public required AuthenticationModeKind Kind { get; set; }
    public required bool IsDefault { get; set; }
    public required List<FieldDescriptor> Fields { get; set; }
}
