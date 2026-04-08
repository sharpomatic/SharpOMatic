namespace SharpOMatic.Engine.Nodes;

public abstract class RunNode<T> : IRunNode
    where T : NodeEntity
{
    protected ThreadContext ThreadContext { get; set; }
    protected T Node { get; init; }
    protected Trace Trace { get; init; }
    protected List<Information> Informations { get; init; }
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

        Informations = [];
    }

    public async Task<NodeExecutionResult> Execute(NodeExecutionRequest request)
    {
        await EnsureRunStarted();
        await NodeRunning();

        try
        {
            var result = request.InvocationKind switch
            {
                NodeInvocationKind.Run => await RunInternal(),
                NodeInvocationKind.Resume => await ResumeInternal(request.ResumeInput ?? throw new SharpOMaticException("Resume input is required.")),
                _ => throw new SharpOMaticException($"Unsupported node invocation kind '{request.InvocationKind}'."),
            };

            if (result.Outcome == NodeExecutionOutcome.Suspend)
                await NodeSuspended(result.Message);
            else
                await NodeSuccess(result.Message);

            return result;
        }
        catch (Exception ex)
        {
            await NodeFailed(ex.Message);
            throw;
        }
    }

    private async Task EnsureRunStarted()
    {
        if (ProcessContext.Run.RunStatus != RunStatus.Created)
            return;

        ProcessContext.Run.RunStatus = RunStatus.Running;
        ProcessContext.Run.Message = "Running";
        ProcessContext.Run.Started ??= DateTime.Now;
        await ProcessContext.RunUpdated();
    }

    protected abstract Task<NodeExecutionResult> RunInternal();

    protected virtual Task<NodeExecutionResult> ResumeInternal(NodeResumeInput input)
    {
        throw new SharpOMaticException($"Node '{Node.NodeType}' does not support conversation resume.");
    }

    protected async Task NodeRunning()
    {
        await ProcessContext.RepositoryService.UpsertTrace(Trace);
        foreach (var progressService in ProcessContext.ProgressServices)
            await progressService.TraceProgress(ProcessContext.Run, Trace);
    }

    protected Task NodeSuccess(string message)
    {
        Trace.NodeStatus = NodeStatus.Success;
        return NodeUpdated(message);
    }

    protected Task NodeSuspended(string message)
    {
        Trace.NodeStatus = NodeStatus.Suspended;
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
        await ProcessContext.RepositoryService.UpsertInformations(Informations);
        foreach (var progressService in ProcessContext.ProgressServices)
        {
            await progressService.TraceProgress(ProcessContext.Run, Trace);
            await progressService.InformationsProgress(ProcessContext.Run, Informations);
        }
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

    protected Task<List<StreamEvent>> AppendStreamEvents(params StreamEventWrite[] events)
    {
        return ProcessContext.AppendStreamEvents(events);
    }
}
