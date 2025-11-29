namespace SharpOMatic.Engine.Nodes;

public abstract class RunNode<T> where T : NodeEntity
{
    protected RunContext RunContext { get; init; }
    protected T Node { get; init; }
    protected Trace Trace { get; init; }

    public RunNode(RunContext runContext, NodeEntity node)
    {
        RunContext = runContext;
        Node = (T)node;

        Trace = new Trace()
        {
            WorkflowId = runContext.Workflow.Id,
            RunId = runContext.RunId,
            TraceId = Guid.NewGuid(),
            NodeEntityId = node.Id,
            Created = DateTime.Now,
            NodeType = node.NodeType,
            NodeStatus = NodeStatus.Running,
            Title = node.Title,
            Message = "Running",
            InputContext = RunContext.TypedSerialization(RunContext.NodeContext)
        };
    }

    public virtual async Task<NodeEntity?> Run()
    {
        await NodeUpdated();
        return null;
    }

    protected async Task NodeUpdated()
    {
        await RunContext.Repository.UpsertTrace(Trace);
        await RunContext.Notifications.TraceProgress(Trace);
    }

    protected Task<object?> EvaluateContextEntryValue(ContextEntryEntity entry)
    {
        return ContextHelpers.EvaluateContextEntryValue(RunContext.NodeContext, entry);
    }
}
