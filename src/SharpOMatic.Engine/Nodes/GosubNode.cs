namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Gosub)]
public class GosubNode(ThreadContext threadContext, GosubNodeEntity node) 
    
    : RunNode<GosubNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        if (Node.WorkflowId is null || Node.WorkflowId == Guid.Empty)
            throw new SharpOMaticException("Gosub node workflow id must be set.");

        var workflow = await ProcessContext.RepositoryService.GetWorkflow(Node.WorkflowId.Value);
        var startNodes = workflow.Nodes.Where(n => n.NodeType == NodeType.Start).ToList();
        if (startNodes.Count != 1)
            throw new SharpOMaticException("Must have exactly one start node.");

        var callerWorkflowContext = ThreadContext.WorkflowContext;
        NodeEntity? returnNode = null;
        if (Node.Outputs.Length > 0)
        {
            if (Node.Outputs.Length != 1)
                throw new SharpOMaticException($"Node must have a single output but found {Node.Outputs.Length}.");

            if (callerWorkflowContext.IsOutputConnected(Node.Outputs[0]))
                returnNode = callerWorkflowContext.ResolveSingleOutput(Node);
        }

        ContextObject childContext;
        if (Node.ApplyInputMappings)
        {
            childContext = [];
            var inputEntries = Node.InputMappings?.Entries ?? [];
            foreach (var entry in inputEntries)
            {
                var inputPath = entry.InputPath;
                var targetPath = string.IsNullOrWhiteSpace(entry.OutputPath) ? inputPath : entry.OutputPath;
                if (string.IsNullOrWhiteSpace(targetPath))
                    throw new SharpOMaticException("Gosub node output path cannot be empty.");

                if (!string.IsNullOrWhiteSpace(inputPath) &&
                    ThreadContext.NodeContext.TryGet<object?>(inputPath, out var mapValue))
                {
                    if (!childContext.TrySet(targetPath, mapValue))
                        throw new SharpOMaticException($"Gosub node input mapping could not set '{targetPath}' into context.");
                }
                else
                {
                    if (!entry.Optional)
                    {
                        if (string.IsNullOrWhiteSpace(inputPath))
                            throw new SharpOMaticException("Gosub node input path cannot be empty.");

                        throw new SharpOMaticException($"Gosub node mandatory path '{inputPath}' cannot be resolved.");
                    }

                    var entryValue = await EvaluateContextEntryValue(entry);
                    if (!childContext.TrySet(targetPath, entryValue))
                        throw new SharpOMaticException($"Gosub node input mapping could not set '{targetPath}' into context.");
                }
            }
        }
        else
        {
            var baseContextJson = ThreadContext.NodeContext.Serialize(ProcessContext.JsonConverters);
            childContext = ContextObject.Deserialize(baseContextJson, ProcessContext.JsonConverters);
        }

        var gosubContext = new GosubContext(ThreadContext.CurrentContext,
                                            Node.Id,
                                            ThreadContext.NodeContext,
                                            returnNode,
                                            Node.ApplyOutputMappings,
                                            Node.OutputMappings);
        gosubContext.IncrementThreads();
        var workflowContext = new WorkflowContext(gosubContext, workflow);
        gosubContext.ChildWorkflowContext = workflowContext;

        ThreadContext.CurrentContext = workflowContext;
        ThreadContext.NodeContext = childContext;

        return ("Gosub started", [new NextNodeData(ThreadContext, startNodes[0])]);
    }
}
