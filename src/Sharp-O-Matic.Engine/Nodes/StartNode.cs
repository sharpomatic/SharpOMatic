namespace SharpOMatic.Engine.Nodes;

public class StartNode(RunContext runContext, StartNodeEntity node) 
    : RunNode<StartNodeEntity>(runContext, node)
{
    public override async Task<NodeEntity?> Run()
    {
        await base.Run();

        try
        {
            var nextNode = RunContext.ResolveSingleOutput(Node);

            if (Node.ApplyInitialization)
            {
                var outputContext = new ContextObject()
                {
                    ["workflowId"] = RunContext.Workflow.Id,
                    ["runId"] = RunContext.RunId
                };

                var provided = 0;
                var defaulted = 0;
                foreach (var entry in Node.Initializing.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.InputPath))
                        throw new SharpOMaticException($"Start node path cannot be empty.");

                    if (RunContext.NodeContext.TryGet<object?>(entry.InputPath, out var mapValue))
                    {
                        outputContext.TrySet(entry.InputPath, mapValue);
                        provided++;
                    }
                    else
                    {
                        if (!entry.Optional)
                            throw new SharpOMaticException($"Start node mandatory path '{entry.InputPath}' cannot be resolved.");

                        var entryValue = await EvaluateContextEntryValue(entry);

                        if (!RunContext.NodeContext.TrySet(entry.InputPath, entryValue))
                            throw new SharpOMaticException($"Start node entry '{entry.InputPath}' could not be assigned the value.");

                        defaulted++;
                    }
                }

                RunContext.NodeContext = outputContext;
                Trace.Message = $"{provided} provided, {defaulted} defaulted";
            }
            else
                Trace.Message = "Entered workflow";

            Trace.NodeStatus = NodeStatus.Success;
            Trace.Finished = DateTime.Now;
            Trace.OutputContext = RunContext.TypedSerialization(RunContext.NodeContext);
            await NodeUpdated();

            return nextNode;
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
