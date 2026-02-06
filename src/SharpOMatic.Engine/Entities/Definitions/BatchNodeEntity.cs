namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.Batch)]
public class BatchNodeEntity : NodeEntity
{
    public required string InputArrayPath { get; set; }
    public required string OutputArrayPath { get; set; }
    public required int BatchSize { get; set; }
    public required int ParallelBatches { get; set; }
}
