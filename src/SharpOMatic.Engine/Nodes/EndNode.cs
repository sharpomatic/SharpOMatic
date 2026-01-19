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

        // Last run EndNode has its output used as the output of the workflow
        ProcessContext.Run.OutputContext = ThreadContext.NodeContext.Serialize(ProcessContext.JsonConverters);

        return (Trace.Message, []);
    }
}
