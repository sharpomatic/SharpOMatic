namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.ActivitySync)]
public sealed class ActivitySyncNode(ThreadContext threadContext, ActivitySyncNodeEntity node) : RunNode<ActivitySyncNodeEntity>(threadContext, node)
{
    protected override async Task<NodeExecutionResult> RunInternal()
    {
        var helper = new StreamEventHelper(ProcessContext, ThreadContext.NodeContext);
        await helper.AddActivitySyncFromContextAsync(Node.InstanceName, Node.ActivityType, Node.ContextPath, Node.InitialReplace, Node.SnapshotsOnly);

        return NodeExecutionResult.Continue($"Activity sync '{Node.InstanceName}' processed.", ResolveOptionalSingleOutput(ThreadContext));
    }
}
