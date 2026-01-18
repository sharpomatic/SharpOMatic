
namespace SharpOMatic.Engine.Contexts;

public class ThreadContext(RunContext runContext, ContextObject nodeContext, ThreadContext? parent = null)
{
    public RunContext RunContext { get; init; } = runContext;
    public ContextObject NodeContext { get; set; } = nodeContext;
    public ThreadContext? Parent { get; init; } = parent;
    public int ThreadId { get; init; } = runContext.GetNextThreadId();
    public int FanOutCount { get; set; }
    public Guid FanInId { get; set; }
    public int FanInArrived { get; set; }
    public ContextObject? FanInMergedContext { get; set; }
}
