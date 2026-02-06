namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Input)]
public class InputNode(ThreadContext threadContext, InputNodeEntity node) : RunNode<InputNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        // Placeholder until input wait/resume is implemented.
        await Task.Delay(1);
        return ("continue", ResolveOptionalSingleOutput(ThreadContext));
    }
}
