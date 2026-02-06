namespace SharpOMatic.Engine.Nodes;

public abstract class RunNode<T> : IRunNode
    where T : NodeEntity
{
    protected ThreadContext ThreadContext { get; set; }
    protected T Node { get; init; }
    protected Trace Trace { get; init; }
    protected ProcessContext ProcessContext => ThreadContext.ProcessContext;
    protected WorkflowContext WorkflowContext => ThreadContext.WorkflowContext;

    public RunNode(ThreadContext threadContext, NodeEntity node)
    {
        ThreadContext = threadContext;
        Node = (T)node;
        var gosubContext = GosubContext.Find(ThreadContext.CurrentContext);

        Trace = new Trace()
        {
            WorkflowId = WorkflowContext.WorkflowId,
            RunId = ProcessContext.Run.RunId,
            TraceId = Guid.NewGuid(),
            NodeEntityId = node.Id,
            ParentTraceId = gosubContext?.ParentTraceId,
            ThreadId = threadContext.ThreadId,
            Created = DateTime.Now,
            NodeType = node.NodeType,
            NodeStatus = NodeStatus.Running,
            Title = node.Title,
            Message = "Running",
            InputContext = ThreadContext.NodeContext.Serialize(ProcessContext.JsonConverters),
        };
    }

    public async Task<List<NextNodeData>> Run()
    {
        await NodeRunning();

        try
        {
            (var message, var nextNodes) = await RunInternal();
            await NodeSuccess(message);
            return nextNodes;
        }
        catch (Exception ex)
        {
            await NodeFailed(ex.Message);
            throw;
        }
    }

    protected abstract Task<(string, List<NextNodeData>)> RunInternal();

    protected async Task NodeRunning()
    {
        await ProcessContext.RepositoryService.UpsertTrace(Trace);
        foreach (var progressService in ProcessContext.ProgressServices)
            await progressService.TraceProgress(Trace);
    }

    protected Task NodeSuccess(string message)
    {
        Trace.NodeStatus = NodeStatus.Success;
        return NodeUpdated(message);
    }

    protected Task NodeFailed(string exception)
    {
        Trace.NodeStatus = NodeStatus.Failed;
        Trace.Error = exception;
        return NodeUpdated("Failed");
    }

    protected async Task NodeUpdated(string message)
    {
        Trace.Finished = DateTime.Now;
        Trace.Message = message;
        Trace.OutputContext = ThreadContext.NodeContext.Serialize(ProcessContext.JsonConverters);
        await ProcessContext.RepositoryService.UpsertTrace(Trace);
        foreach (var progressService in ProcessContext.ProgressServices)
            await progressService.TraceProgress(Trace);
    }

    protected List<NextNodeData> ResolveOptionalSingleOutput(ThreadContext nextThreadContext)
    {
        if (Node.Outputs.Length == 0)
            return [];

        if (Node.Outputs.Length != 1)
            throw new SharpOMaticException($"Node must have a single output but found {Node.Outputs.Length}.");

        if (!IsOutputConnected(Node.Outputs[0]))
            return [];

        return [new NextNodeData(nextThreadContext, WorkflowContext.ResolveSingleOutput(Node))];
    }

    protected bool IsOutputConnected(ConnectorEntity connector)
    {
        return WorkflowContext.IsOutputConnected(connector);
    }

    protected Task<object?> EvaluateContextEntryValue(ContextEntryEntity entry)
    {
        return ContextHelpers.ResolveContextEntryValue(ProcessContext.ServiceScope.ServiceProvider, ThreadContext.NodeContext, entry, ProcessContext.ScriptOptionsService, ProcessContext.Run.RunId);
    }
}
