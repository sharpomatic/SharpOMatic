namespace SharpOMatic.Engine.Nodes;

public class ModelCallNode(ThreadContext threadContext, ModelCallNodeEntity node) : RunNode<ModelCallNodeEntity>(threadContext, node)
{
    protected override Task<(string, List<NextNodeData>)> RunInternal()
    {
        return Task.FromResult(("Model call executed", new List<NextNodeData> { new(ThreadContext, RunContext.ResolveSingleOutput(Node)) }));
    }
}
