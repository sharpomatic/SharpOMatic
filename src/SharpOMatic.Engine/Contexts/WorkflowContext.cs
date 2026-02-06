namespace SharpOMatic.Engine.Contexts;

public sealed class WorkflowContext : ExecutionContext
{
    private readonly Dictionary<Guid, NodeEntity> _outputConnectorToNode = [];
    private readonly Dictionary<Guid, NodeEntity> _inputConnectorToNode = [];
    private readonly Dictionary<Guid, ConnectionEntity> _fromToConnection = [];

    public WorkflowEntity Workflow { get; }
    public Guid WorkflowId => Workflow.Id;

    public WorkflowContext(ExecutionContext parent, WorkflowEntity workflow)
        : base(parent)
    {
        Workflow = workflow;

        foreach (var node in workflow.Nodes)
        {
            foreach (var connector in node.Outputs)
                _outputConnectorToNode.Add(connector.Id, node);

            foreach (var connector in node.Inputs)
                _inputConnectorToNode.Add(connector.Id, node);
        }

        _fromToConnection = workflow.Connections.ToDictionary(c => c.From, c => c);
    }

    public NodeEntity ResolveSingleOutput(NodeEntity node)
    {
        if (node.Outputs.Length != 1)
            throw new SharpOMaticException($"Node must have a single output but found {node.Outputs.Length}.");

        return ResolveOutput(node.Outputs[0]);
    }

    public NodeEntity ResolveOutput(ConnectorEntity connector)
    {
        if (!_fromToConnection.TryGetValue(connector.Id, out var connection) || !_inputConnectorToNode.TryGetValue(connection.To, out var nextNode))
        {
            if (string.IsNullOrWhiteSpace(connector.Name))
                throw new SharpOMaticException($"Cannot traverse '{connector.Name}' output because it is not connected to another node.");
            else
                throw new SharpOMaticException($"Cannot traverse output because it is not connected to another node.");
        }

        return nextNode;
    }

    public bool IsOutputConnected(ConnectorEntity connector)
    {
        return _fromToConnection.ContainsKey(connector.Id);
    }
}
