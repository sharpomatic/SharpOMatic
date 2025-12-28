namespace SharpOMatic.Engine.Nodes;

public abstract class RunNode<T> : IRunNode where T : NodeEntity
{
    protected ThreadContext ThreadContext { get; set; }
    protected T Node { get; init; }
    protected Trace Trace { get; init; }
    protected RunContext RunContext => ThreadContext.RunContext;

    public RunNode(ThreadContext threadContext, NodeEntity node)
    {
        ThreadContext = threadContext;
        Node = (T)node;

        Trace = new Trace()
        {
            WorkflowId = RunContext.Workflow.Id,
            RunId = RunContext.Run.RunId,
            TraceId = Guid.NewGuid(),
            NodeEntityId = node.Id,
            Created = DateTime.Now,
            NodeType = node.NodeType,
            NodeStatus = NodeStatus.Running,
            Title = node.Title,
            Message = "Running",
            InputContext = RunContext.TypedSerialization(ThreadContext.NodeContext)
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
        await RunContext.RepositoryService.UpsertTrace(Trace);
        await RunContext.ProgressService.TraceProgress(Trace);
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
        Trace.OutputContext = RunContext.TypedSerialization(ThreadContext.NodeContext);
        await RunContext.RepositoryService.UpsertTrace(Trace);
        await RunContext.ProgressService.TraceProgress(Trace);
    }

    protected Task<object?> EvaluateContextEntryValue(ContextEntryEntity entry)
    {
        return ContextHelpers.ResolveContextEntryValue(ThreadContext.NodeContext, entry, RunContext.ScriptOptionsService);
    }
}
