namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Batch)]
public class BatchNode(ThreadContext threadContext, BatchNodeEntity node) : RunNode<BatchNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        await Task.Delay(1);
        var nextNode = RunContext.ResolveOutput(Node.Outputs[0]);
        return ("continue", [new NextNodeData(ThreadContext, nextNode)]);
    }
}
