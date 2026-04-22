namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.ActivitySync)]
public sealed class ActivitySyncNodeEntity : NodeEntity
{
    public required string InstanceName { get; set; }
    public required string ActivityType { get; set; }
    public required string ContextPath { get; set; }
    public required bool InitialReplace { get; set; }
}
