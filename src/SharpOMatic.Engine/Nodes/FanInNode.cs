namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.FanIn)]
public class FanInNode(ThreadContext threadContext, FanInNodeEntity node) 
    : RunNode<FanInNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        if (ThreadContext.CurrentContext is not FanOutInContext fanOutContext)
            throw new SharpOMaticException($"Arriving thread did not originate from a Fan Out.");

        // If multiple threads arrive at this node at the same time, serialize so they merge correctly
        lock (fanOutContext)
        {
            if (fanOutContext.FanInId is null)
            {
                // First thread from a FanOut to arrive at this FanIn
                fanOutContext.FanInId = Node.Id;
            }
            else if (fanOutContext.FanInId != Node.Id)
            {
                // This thread is arriving at a different FanIn than another thread from the same FanOut
                throw new SharpOMaticException($"All incoming connections must originate from the same Fan Out.");
            }

            if (ThreadContext.NodeContext.TryGetValue("output", out var outputValue))
            {
                var tempContext = new ContextObject { { "output", outputValue } };
                if (fanOutContext.MergedContext is null)
                    throw new SharpOMaticException("Fan out context is missing a merge target.");

                ProcessContext.MergeContexts(fanOutContext.MergedContext, tempContext);
            }

            fanOutContext.FanInArrived++;
            if (fanOutContext.FanInArrived < fanOutContext.FanOutCount)
            {
                // Exit thread, exited wait for other threads to arrive
                return ("Thread arrived", []);
            }
            else
            {
                // Last thread to arrive; exit fan-out scope and continue with merged context
                ThreadContext.NodeContext = fanOutContext.MergedContext ?? ThreadContext.NodeContext;
                if (fanOutContext.Parent is null)
                    throw new SharpOMaticException("Fan out context is missing a parent execution context.");

                ThreadContext.CurrentContext = fanOutContext.Parent;
                ProcessContext.UntrackContext(fanOutContext);
                return ("Threads synchronized", ResolveOptionalSingleOutput(ThreadContext));
            }
        }
    }
}
