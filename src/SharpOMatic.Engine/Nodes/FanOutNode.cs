namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.FanOut)]
public class FanOutNode(ThreadContext threadContext, FanOutNodeEntity node) : RunNode<FanOutNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        var json = ThreadContext.NodeContext.Serialize(ProcessContext.JsonConverters);

        var connectedOutputs = Node.Outputs.Where(IsOutputConnected).ToList();
        if (connectedOutputs.Count == 0)
            return ("No outputs connected", []);

        var fanOutContext = new FanOutInContext(ThreadContext.CurrentContext)
        {
            FanOutCount = connectedOutputs.Count,
            FanInArrived = 0,
            FanInId = null,
            MergedContext = ContextObject.Deserialize(json, ProcessContext.JsonConverters),
        };

        List<NextNodeData> nextNodes = [];
        foreach (var connector in connectedOutputs)
        {
            var resolveNode = WorkflowContext.ResolveOutput(connector);
            var newContext = ContextObject.Deserialize(json, ProcessContext.JsonConverters);
            var newThreadContext = ProcessContext.CreateThread(newContext, fanOutContext);
            newThreadContext.BatchIndex = ThreadContext.BatchIndex;
            nextNodes.Add(new NextNodeData(newThreadContext, resolveNode));
        }

        var message = $"{nextNodes.Count} threads started";
        return (message, nextNodes);
    }
}
