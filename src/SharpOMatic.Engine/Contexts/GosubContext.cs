namespace SharpOMatic.Engine.Contexts;

public sealed class GosubContext : ExecutionContext
{
    private int _activeThreads;
    private readonly object _mergeLock = new();

    public GosubContext(
        ExecutionContext parent,
        Guid parentTraceId,
        ContextObject parentContext,
        NodeEntity? returnNode,
        bool applyOutputMappings,
        ContextEntryListEntity outputMappings) : base(parent)
    {
        ParentTraceId = parentTraceId;
        ParentContext = parentContext;
        ReturnNode = returnNode;
        ApplyOutputMappings = applyOutputMappings;
        OutputMappings = outputMappings;
    }

    public Guid ParentTraceId { get; }
    public ContextObject ParentContext { get; }
    public NodeEntity? ReturnNode { get; }
    public WorkflowContext? ChildWorkflowContext { get; set; }
    public bool ApplyOutputMappings { get; }
    public ContextEntryListEntity OutputMappings { get; }
    public object MergeLock => _mergeLock;
    public void MergeOutput(ProcessContext processContext, ContextObject childContext)
    {
        lock (MergeLock)
        {
            if (ApplyOutputMappings)
            {
                var outputContext = new ContextObject();
                var mappings = OutputMappings?.Entries ?? [];
                foreach (var mapping in mappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.InputPath))
                        throw new SharpOMaticException("Gosub output mapping input path cannot be empty.");

                    if (string.IsNullOrWhiteSpace(mapping.OutputPath))
                        throw new SharpOMaticException("Gosub output mapping output path cannot be empty.");

                    if (childContext.TryGet<object?>(mapping.InputPath, out var mapValue))
                    {
                        if (!outputContext.TrySet(mapping.OutputPath, mapValue))
                            throw new SharpOMaticException($"Gosub output mapping could not set '{mapping.OutputPath}' into context.");
                    }
                }

                processContext.MergeContextsOverwrite(ParentContext, outputContext);
            }
            else
            {
                processContext.MergeContextsOverwrite(ParentContext, childContext);
            }
        }
    }

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
