namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.StateSync)]
public sealed class StateSyncNodeEntity : NodeEntity
{
    public bool SnapshotsOnly { get; set; }
}
