namespace SharpOMatic.Engine.Contexts;

public sealed class BatchContext : ExecutionContext
{
    public BatchContext(ExecutionContext parent) : base(parent)
    {
    }

    public Guid FanOutId { get; set; }
    public ContextList BatchItems { get; set; } = [];
    public int BatchSize { get; set; }
    public int ParallelBatches { get; set; }
    public int NextItemIndex { get; set; }
    public int InFlightBatches { get; set; }
    public int CompletedBatches { get; set; }
    public Guid BatchNodeId { get; set; }
    public Guid? ContinueNodeId { get; set; }
}
