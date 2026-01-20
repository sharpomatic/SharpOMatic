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

        var baseContextJson = ThreadContext.NodeContext.Serialize(ProcessContext.JsonConverters);
        var childContext = ContextObject.Deserialize(baseContextJson, ProcessContext.JsonConverters);

        var gosubContext = new GosubContext(ThreadContext.CurrentContext, ThreadContext.NodeContext, returnNode);
        gosubContext.IncrementThreads();
        var workflowContext = new WorkflowContext(gosubContext, workflow);
        gosubContext.ChildWorkflowContext = workflowContext;

        ThreadContext.CurrentContext = workflowContext;
        ThreadContext.NodeContext = childContext;

        return ("Gosub started", [new NextNodeData(ThreadContext, startNodes[0])]);
    }
}
