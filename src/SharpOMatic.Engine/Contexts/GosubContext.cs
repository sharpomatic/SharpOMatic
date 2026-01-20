namespace SharpOMatic.Engine.Contexts;

public sealed class GosubContext : ExecutionContext
{
    private int _activeThreads;
    private readonly object _mergeLock = new();

    public GosubContext(ExecutionContext parent, ContextObject parentContext, NodeEntity? returnNode) : base(parent)
    {
        ParentContext = parentContext;
        ReturnNode = returnNode;
    }

    public ContextObject ParentContext { get; }
    public NodeEntity? ReturnNode { get; }
    public WorkflowContext? ChildWorkflowContext { get; set; }
    public object MergeLock => _mergeLock;

    public int IncrementThreads() => Interlocked.Increment(ref _activeThreads);

    public int DecrementThreads() => Interlocked.Decrement(ref _activeThreads);

    public int ActiveThreads => Volatile.Read(ref _activeThreads);

    public static GosubContext? Find(ExecutionContext context)
    {
        var current = context;
        while (current is not null)
        {
            if (current is GosubContext gosubContext)
                return gosubContext;

            current = current.Parent;
        }

        return null;
    }
}
