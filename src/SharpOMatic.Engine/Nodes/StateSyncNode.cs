namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.StateSync)]
public sealed class StateSyncNode(ThreadContext threadContext, StateSyncNodeEntity node) : RunNode<StateSyncNodeEntity>(threadContext, node)
{
    protected override async Task<NodeExecutionResult> RunInternal()
    {
        var helper = new StreamEventHelper(ProcessContext, ThreadContext.NodeContext);
        await helper.AddStateSyncAsync(Node.SnapshotsOnly);

        return NodeExecutionResult.Continue("State sync processed.", ResolveOptionalSingleOutput(ThreadContext));
    }
}
