namespace SharpOMatic.Engine.Contexts;

public class RunContext
{
    private readonly Dictionary<Guid, NodeEntity> _ouputConnectorToNode = [];
    private readonly Dictionary<Guid, NodeEntity> _inputConnectorToNode = [];
    private readonly Dictionary<Guid, ConnectionEntity> _fromToConnection = [];

    public ISharpOMaticRepository Repository { get; init; }
    public ISharpOMaticNotification Notifications { get; init; }
    public IEnumerable<JsonConverter> JsonConverters { get; init; }
    public WorkflowEntity Workflow { get; init; }
    public Guid RunId { get; init; }
    public ContextObject NodeContext { get; set; }

    public RunContext(ISharpOMaticRepository repository, 
                      ISharpOMaticNotification notifications,
                      IEnumerable<JsonConverter> jsonConverters,
                      WorkflowEntity workflow, 
                      Guid runId,
                      ContextObject nodeContext)
    {
        Repository = repository;
        Notifications = notifications;
        JsonConverters = jsonConverters;
        Workflow = workflow;
        RunId = runId;
        NodeContext = nodeContext;

        foreach (var node in workflow.Nodes)
        {
            foreach (var connector in node.Outputs)
                _ouputConnectorToNode.Add(connector.Id, node);

            foreach (var connector in node.Inputs)
                _inputConnectorToNode.Add(connector.Id, node);
        }

        _fromToConnection = workflow.Connections.ToDictionary(c => c.From, c => c);
    }

    public NodeEntity? ResolveSingleOutput(NodeEntity node)
    {
        if (node.Outputs.Length != 1)
            throw new SharpOMaticException($"Node must have a single output but found {node.Outputs.Length}.");

        return ResolveOutput(node, node.Outputs[0]);
    }

    public NodeEntity? ResolveOutput(NodeEntity node, ConnectorEntity connector)
    {
        if (!_fromToConnection.TryGetValue(connector.Id, out var connection) ||
            !_inputConnectorToNode.TryGetValue(connection.To, out var nextNode))
        {
            if (connector.Name is not null)
                throw new SharpOMaticException($"Cannot traverse '{connector.Name}' output because it is not connected to another node.");
            else
                throw new SharpOMaticException($"Cannot traverse output because it is not connected to another node.");
        }

        return nextNode;
    }

    public string TypedSerialization(ContextObject nodeContext)
    {
        return JsonSerializer.Serialize(nodeContext, new JsonSerializerOptions().BuildOptions(JsonConverters));
    }
}
