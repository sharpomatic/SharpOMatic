namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.FanIn)]
public class FanInNode(ThreadContext threadContext, FanInNodeEntity node) : RunNode<FanInNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        if (ThreadContext.Parent is null)
            throw new SharpOMaticException($"Arriving thread did not originate from a Fan Out.");

        // If multiple threads arrive at this node at the same time, serialize so they merge correctly
        lock (ThreadContext.Parent)
        {
            if (ThreadContext.Parent.FanInId == Guid.Empty)
            {
                // First thread from a FanOut to arrive at this FanIn
                ThreadContext.Parent.FanInId = Node.Id;
            }
            else if (ThreadContext.Parent.FanInId != Node.Id)
            {
                // This thread is arriving at a different FanIn than another thread from the same FanOut
                throw new SharpOMaticException($"All incoming connections must originate from the same Fan Out.");
            }

            if (ThreadContext.Parent.FanInMergedContext is null)
            {
                var json = ThreadContext.Parent.NodeContext.Serialize(RunContext.JsonConverters);
                ThreadContext.Parent.FanInMergedContext = ContextObject.Deserialize(json, RunContext.JsonConverters);
            }

            if (ThreadContext.NodeContext.TryGetValue("output", out var outputValue))
            {
                var tempContext = new ContextObject { { "output", outputValue } };
                ThreadContext.RunContext.MergeContexts(ThreadContext.Parent.FanInMergedContext, tempContext);
            }

            ThreadContext.Parent.FanInArrived++;
            if (ThreadContext.Parent.FanInArrived < ThreadContext.Parent.FanOutCount)
            {
                // Exit thread, exited wait for other threads to arrive
                return ("Thread arrived", []);
            }
            else
            {
                ThreadContext.Parent.FanInArrived = 0;
                ThreadContext.Parent.FanOutCount = 0;
                ThreadContext.Parent.FanInId = Guid.Empty;

                // Must set the merged context into this thread, so tracing progresses merges
                ThreadContext.NodeContext = ThreadContext.Parent.FanInMergedContext;
                ThreadContext.Parent.NodeContext = ThreadContext.NodeContext;
                return ("Threads synchronized", ResolveOptionalSingleOutput(ThreadContext.Parent));
            }
        }
    }
}
