namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Suspend)]
public class SuspendNode(ThreadContext threadContext, SuspendNodeEntity node) : RunNode<SuspendNodeEntity>(threadContext, node)
{
    protected override Task<NodeExecutionResult> RunInternal()
    {
        return Task.FromResult(NodeExecutionResult.Suspend("Suspended"));
    }

    protected override Task<NodeExecutionResult> ResumeInternal(NodeResumeInput input)
    {
        switch (input)
        {
            case ContinueResumeInput:
                break;
            case ContextMergeResumeInput contextMerge:
                ContextHelpers.OverwriteContexts(ThreadContext.NodeContext, contextMerge.Context);
                break;
            default:
                throw new SharpOMaticException($"Suspend node cannot handle resume input type '{input.GetType().Name}'.");
        }

        return Task.FromResult(NodeExecutionResult.Continue("Resumed", ResolveOptionalSingleOutput(ThreadContext)));
    }
}
