namespace SharpOMatic.Engine.Contexts;

public sealed class BatchContext : ExecutionContext
{
    public BatchContext(ExecutionContext parent) : base(parent)
    {
    }

    public Guid FanOutId { get; set; }
    public string InputArrayPath { get; set; } = string.Empty;
    public string? BaseContextJson { get; set; }
    public ContextObject? MergedContext { get; set; }
    public Dictionary<int, ContextObject> BatchOutputs { get; } = new();
    public ContextList BatchItems { get; set; } = [];
    public int BatchSize { get; set; }
    public int ParallelBatches { get; set; }
    public int NextItemIndex { get; set; }
    public int NextBatchIndex { get; set; }
    public int InFlightBatches { get; set; }
    public int CompletedBatches { get; set; }
    public Guid BatchNodeId { get; set; }
    public Guid? ContinueNodeId { get; set; }
    public NodeEntity? ContinueNode { get; set; }
    public NodeEntity? ProcessNode { get; set; }
}
