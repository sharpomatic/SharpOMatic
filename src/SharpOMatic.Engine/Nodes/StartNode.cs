namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Start)]
public class StartNode(ThreadContext threadContext, StartNodeEntity node)
    : RunNode<StartNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        // Record when the workflow is running because the start node is processing
        if (ProcessContext.Run.RunStatus == RunStatus.Created)
        {
            ProcessContext.Run.RunStatus = RunStatus.Running;
            ProcessContext.Run.Message = "Running";
            ProcessContext.Run.Started = DateTime.Now;
            await ProcessContext.RunUpdated();
        }

        if (Node.ApplyInitialization)
        {
            var outputContext = new ContextObject();

            var provided = 0;
            var defaulted = 0;
            foreach (var entry in Node.Initializing.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.InputPath))
                    throw new SharpOMaticException($"Start node path cannot be empty.");

                if (ThreadContext.NodeContext.TryGet<object?>(entry.InputPath, out var mapValue))
                {
                    if (!outputContext.TrySet(entry.InputPath, mapValue))
                        throw new SharpOMaticException(
                            $"Start node cannot set '{entry.InputPath}' into context."
                        );

                    provided++;
                }
                else
                {
                    if (!entry.Optional)
                        throw new SharpOMaticException(
                            $"Start node mandatory path '{entry.InputPath}' cannot be resolved."
                        );

                    var entryValue = await EvaluateContextEntryValue(entry);

                    if (!outputContext.TrySet(entry.InputPath, entryValue))
                        throw new SharpOMaticException(
                            $"Start node entry '{entry.InputPath}' could not be assigned the value."
                        );

                    defaulted++;
                }
            }

            ThreadContext.NodeContext = outputContext;
            Trace.Message = $"{provided} provided, {defaulted} defaulted";
        }
        else
            Trace.Message = "Entered workflow";

        return (Trace.Message, ResolveOptionalSingleOutput(ThreadContext));
    }
}
