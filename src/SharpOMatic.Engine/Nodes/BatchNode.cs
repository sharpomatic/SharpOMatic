namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Batch)]
public class BatchNode(ThreadContext threadContext, BatchNodeEntity node) : RunNode<BatchNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        if (Node.BatchSize < 1)
            throw new SharpOMaticException("Batch node batch size must be greater than or equal to 1.");

        if (Node.ParallelBatches < 1)
            throw new SharpOMaticException("Batch node parallel batches must be greater than or equal to 1.");

        if (string.IsNullOrWhiteSpace(Node.InputArrayPath))
            throw new SharpOMaticException("Batch node input array path cannot be empty.");

        if (!ThreadContext.NodeContext.TryGet<object?>(Node.InputArrayPath, out var arrayValue) || arrayValue is null)
            throw new SharpOMaticException($"Batch node input array path '{Node.InputArrayPath}' could not be resolved.");

        if (arrayValue is not ContextList arrayList)
            throw new SharpOMaticException($"Batch node input array path '{Node.InputArrayPath}' must be a context list.");

        var baseContextJson = ThreadContext.NodeContext.Serialize(ProcessContext.JsonConverters);

        NodeEntity? continueNode = null;
        if (Node.Outputs.Length > 0 && IsOutputConnected(Node.Outputs[0]))
            continueNode = WorkflowContext.ResolveOutput(Node.Outputs[0]);

        NodeEntity? processNode = null;
        if (Node.Outputs.Length > 1 && IsOutputConnected(Node.Outputs[1]))
            processNode = WorkflowContext.ResolveOutput(Node.Outputs[1]);

        if (processNode is null || arrayList.Count == 0)
        {
            if (continueNode is null)
                return ("continue, no items", []);

            ThreadContext.NodeContext = ContextObject.Deserialize(baseContextJson, ProcessContext.JsonConverters);
            return ("continue, no items", [new NextNodeData(ThreadContext, continueNode)]);
        }

        var batchContext = new BatchContext(ThreadContext.CurrentContext)
        {
            InputArrayPath = Node.InputArrayPath,
            BaseContextJson = baseContextJson,
            BatchItems = arrayList,
            BatchSize = Node.BatchSize,
            ParallelBatches = Node.ParallelBatches,
            NextItemIndex = 0,
            NextBatchIndex = 0,
            InFlightBatches = 0,
            CompletedBatches = 0,
            BatchNodeId = Node.Id,
            ContinueNodeId = continueNode?.Id,
            ContinueNode = continueNode,
            ProcessNode = processNode,
        };

        ThreadContext.CurrentContext = batchContext;
        List<NextNodeData> nextNodes = [];

        while (batchContext.InFlightBatches < batchContext.ParallelBatches && batchContext.NextItemIndex < batchContext.BatchItems.Count)
        {
            var batchSlice = CreateBatchSlice(batchContext.BatchItems, batchContext.NextItemIndex, batchContext.BatchSize);
            if (batchSlice.Count == 0)
                break;

            var batchIndex = batchContext.NextBatchIndex++;
            batchContext.NextItemIndex += batchSlice.Count;
            batchContext.InFlightBatches++;

            ThreadContext targetThread;
            if (nextNodes.Count == 0)
            {
                ThreadContext.NodeContext = CreateBatchContextObject(batchContext.BaseContextJson, batchSlice);
                ThreadContext.BatchIndex = batchIndex;
                targetThread = ThreadContext;
            }
            else
            {
                var batchContextObject = CreateBatchContextObject(batchContext.BaseContextJson, batchSlice);
                targetThread = ProcessContext.CreateThread(batchContextObject, batchContext);
                targetThread.BatchIndex = batchIndex;
            }

            nextNodes.Add(new NextNodeData(targetThread, processNode));
        }

        if (nextNodes.Count == 0)
        {
            if (continueNode is null)
                return ("continue, no items", []);

            ThreadContext.NodeContext = ContextObject.Deserialize(baseContextJson, ProcessContext.JsonConverters);
            return ("continue, no items", [new NextNodeData(ThreadContext, continueNode)]);
        }

        return ($"process {batchContext.BatchItems.Count}", nextNodes);
    }

    private ContextObject CreateBatchContextObject(string? contextJson, ContextList slice)
    {
        var context = ContextObject.Deserialize(contextJson, ProcessContext.JsonConverters);
        if (!context.TrySet(Node.InputArrayPath, slice))
            throw new SharpOMaticException($"Batch node cannot set '{Node.InputArrayPath}' into context.");

        return context;
    }

    private static ContextList CreateBatchSlice(ContextList items, int startIndex, int batchSize)
    {
        var slice = new ContextList();
        var endIndex = Math.Min(items.Count, startIndex + batchSize);
        for (var index = startIndex; index < endIndex; index++)
            slice.Add(items[index]);

        return slice;
    }
}
