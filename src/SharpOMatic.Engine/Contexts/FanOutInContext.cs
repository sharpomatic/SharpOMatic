namespace SharpOMatic.Engine.Contexts;

public sealed class FanOutInContext : ExecutionContext
{
    public FanOutInContext(ExecutionContext parent)
        : base(parent) { }

    public int FanOutCount { get; set; }
    public Guid? FanInId { get; set; }
    public int FanInArrived { get; set; }
    public ContextObject? MergedContext { get; set; }
}
