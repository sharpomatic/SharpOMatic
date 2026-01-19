namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Gosub)]
public class GosubNode(ThreadContext threadContext, GosubNodeEntity node) 
    
    : RunNode<GosubNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        // Placeholder until gosub execution is implemented.
        await Task.Delay(1);

        if (Node.WorkflowId is null || Node.WorkflowId == Guid.Empty)
            throw new SharpOMaticException("Gosub node workflow id must be set.");

        return ("continue", ResolveOptionalSingleOutput(ThreadContext));
    }
}
