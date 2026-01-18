namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.FanOut)]
public class FanOutNode(ThreadContext threadContext, FanOutNodeEntity node) : RunNode<FanOutNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        var json = ThreadContext.NodeContext.Serialize(RunContext.JsonConverters);

        List<NextNodeData> nextNodes = [];
        foreach (var connector in Node.Outputs)
        {
            if (!IsOutputConnected(connector))
                continue;

            var resolveNode = RunContext.ResolveOutput(connector);
            var newContext = ContextObject.Deserialize(json, RunContext.JsonConverters);
            var newThreadContext = new ThreadContext(RunContext, newContext, ThreadContext);
            nextNodes.Add(new NextNodeData(newThreadContext, resolveNode));
        }

        ThreadContext.FanOutCount = nextNodes.Count;
        ThreadContext.FanInArrived = 0;

        var message = nextNodes.Count == 0
            ? "No outputs connected"
            : $"{nextNodes.Count} threads started";

        return (message, nextNodes);
    }
}
