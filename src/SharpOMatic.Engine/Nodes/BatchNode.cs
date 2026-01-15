namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Batch)]
public class BatchNode(ThreadContext threadContext, BatchNodeEntity node) : RunNode<BatchNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        // TEMP, until it does something async
        await Task.Delay(1);

        if (Node.BatchSize < 0)
            throw new SharpOMaticException("Batch node batch size must be greater than or equal to 0.");

        if (Node.ParallelBatches < 1)
            throw new SharpOMaticException("Batch node parallel batches must be greater than or equal to 1.");

        if (string.IsNullOrWhiteSpace(Node.InputArrayPath))
            throw new SharpOMaticException("Batch node input array path cannot be empty.");

        if (!ThreadContext.NodeContext.TryGet<object?>(Node.InputArrayPath, out var arrayValue) || arrayValue is null)
            throw new SharpOMaticException($"Batch node input array path '{Node.InputArrayPath}' could not be resolved.");

        if (arrayValue is not ContextList)
            throw new SharpOMaticException($"Batch node input array path '{Node.InputArrayPath}' must be a context list.");

        var nextNode = RunContext.ResolveOutput(Node.Outputs[0]);
        return ("continue", [new NextNodeData(ThreadContext, nextNode)]);
    }
}
