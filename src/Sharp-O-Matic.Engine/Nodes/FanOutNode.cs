namespace SharpOMatic.Engine.Nodes;

public class FanOutNode(ThreadContext threadContext, FanOutNodeEntity node) : RunNode<FanOutNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        var resolveNodes = RunContext.ResolveMultipleOutputs(Node);
        var json = RunContext.TypedSerialization(ThreadContext.NodeContext);

        List<NextNodeData> nextNodes = [];
        foreach(var resolveNode in resolveNodes)
        {
            var newContext = RunContext.TypedDeserialization(json);
            var newThreadContext = new ThreadContext(RunContext, newContext, ThreadContext);
            nextNodes.Add(new NextNodeData(newThreadContext, resolveNode));
        }

        ThreadContext.FanOutCount = nextNodes.Count;
        ThreadContext.FanInArrived = 0;

        return ($"{nextNodes.Count} threads started", nextNodes);
    }
}
