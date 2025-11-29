using Analyzer.Utilities.Options;
using SharpOMatic.Engine.Entities;
using SharpOMatic.Engine.Repository;
using System.Reflection;
using System.Text;

namespace SharpOMatic.Engine.Nodes;

public class EndNode(RunContext runContext, EndNodeEntity node)
    : RunNode<EndNodeEntity>(runContext, node)
{
    public override async Task<NodeEntity?> Run()
    {
        await base.Run();

        try
        {
            if (Node.ApplyMappings)
            {
                var outputContext = new ContextObject()
                {
                    ["workflowId"] = RunContext.Workflow.Id,
                    ["runId"] = RunContext.RunId
                };

                var mapped = 0;
                var missing = 0;
                foreach (var mapping in Node.Mappings.Entries)
                {
                    if (string.IsNullOrWhiteSpace(mapping.InputPath))
                        throw new SharpOMaticException($"End node input path cannot be empty.");

                    if (string.IsNullOrWhiteSpace(mapping.OutputPath))
                        throw new SharpOMaticException($"End node output path cannot be empty.");

                    if (RunContext.NodeContext.TryGet<object?>(mapping.InputPath, out var mapValue))
                    {
                        outputContext.TrySet(mapping.OutputPath, mapValue);
                        mapped++;
                    }
                    else
                        missing++;
                }

                RunContext.NodeContext = outputContext;
                Trace.Message = $"{mapped} mapped, {missing} missing";
            }
            else
                Trace.Message = "Exited workflow";

            Trace.NodeStatus = NodeStatus.Success;
            Trace.Finished = DateTime.Now;
            Trace.OutputContext = RunContext.TypedSerialization(RunContext.NodeContext);
            await NodeUpdated();

            return null;
        }
        catch (Exception ex)
        {
            Trace.NodeStatus = NodeStatus.Failed;
            Trace.Finished = DateTime.Now;
            Trace.Message = "Failed";
            Trace.Error = ex.Message;
            Trace.OutputContext = RunContext.TypedSerialization(RunContext.NodeContext);
            await NodeUpdated();

            throw;
        }
    }
}
