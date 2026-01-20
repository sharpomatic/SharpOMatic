using System.Text.Json.Serialization;

namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.End)]
public class EndNode(ThreadContext threadContext, EndNodeEntity node) 
    : RunNode<EndNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        if (Node.ApplyMappings)
        {
            var outputContext = new ContextObject();

            var mapped = 0;
            var missing = 0;
            foreach (var mapping in Node.Mappings.Entries)
            {
                if (string.IsNullOrWhiteSpace(mapping.InputPath))
                    throw new SharpOMaticException($"End node input path cannot be empty.");

                if (string.IsNullOrWhiteSpace(mapping.OutputPath))
                    throw new SharpOMaticException($"End node output path cannot be empty.");

                if (ThreadContext.NodeContext.TryGet<object?>(mapping.InputPath, out var mapValue))
                {
                    outputContext.TrySet(mapping.OutputPath, mapValue);
                    mapped++;
                }
                else
                    missing++;
            }

            ThreadContext.NodeContext = outputContext;
            Trace.Message = $"{mapped} mapped, {missing} missing";
        }
        else
            Trace.Message = "Exited workflow";

        var gosubContext = GosubContext.Find(ThreadContext.CurrentContext);
        if (gosubContext is not null)
        {
            if (gosubContext.Parent is null)
                throw new SharpOMaticException("Gosub context is missing a parent execution context.");

            lock (gosubContext.MergeLock)
            {
                gosubContext.MergeOutput(ProcessContext, ThreadContext.NodeContext);
            }

            ThreadContext.NodeContext = gosubContext.ParentContext;
            ThreadContext.CurrentContext = gosubContext.Parent;

            if (gosubContext.DecrementThreads() == 0)
            {
                ProcessContext.UntrackContext(gosubContext);
                if (gosubContext.ChildWorkflowContext is not null)
                    ProcessContext.UntrackContext(gosubContext.ChildWorkflowContext);
            }

            if (gosubContext.ReturnNode is null)
                return (Trace.Message, []);

            return (Trace.Message, [new NextNodeData(ThreadContext, gosubContext.ReturnNode)]);
        }

        // Last run EndNode has its output used as the output of the workflow
        ProcessContext.Run.OutputContext = ThreadContext.NodeContext.Serialize(ProcessContext.JsonConverters);

        return (Trace.Message, []);
    }
}
