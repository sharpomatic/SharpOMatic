using SharpOMatic.Engine.Services;

namespace SharpOMatic.Engine.Contexts;

public class RunContext
{
    private readonly Dictionary<Guid, NodeEntity> _ouputConnectorToNode = [];
    private readonly Dictionary<Guid, NodeEntity> _inputConnectorToNode = [];
    private readonly Dictionary<Guid, ConnectionEntity> _fromToConnection = [];
    private TaskCompletionSource<Run>? _completionSource;

    private int _threadId = 1;
    private int _threadCount = 1;
    private int _nodesRun = 0;
    private int _runNodeLimit = 0;

    public IServiceScope ServiceScope { get; init; }
    public IRepositoryService RepositoryService { get; init; }
    public IProgressService ProgressService { get; init; }
    public IToolMethodRegistry ToolMethodRegistry { get; init; }
    public ISchemaTypeRegistry SchemaTypeRegistry { get; init; }
    public IScriptOptionsService ScriptOptionsService { get; init; }
    public IEnumerable<JsonConverter> JsonConverters { get; init; }
    public WorkflowEntity Workflow { get; init; }
    public Run Run { get; init; }
    public int RunningThreadCount => _threadCount;
    public int NodesRun => Volatile.Read(ref _nodesRun);
    public int RunNodeLimit => _runNodeLimit;
    public TaskCompletionSource<Run>? CompletionSource { get; init; }

    public RunContext(IServiceScope serviceScope,
                      IRepositoryService repositoryService,
                      IProgressService progressService,
                      IToolMethodRegistry toolMethodRegistry,
                      ISchemaTypeRegistry schemaTypeRegistry,
                      IScriptOptionsService scriptOptionsService,
                      IEnumerable<JsonConverter> jsonConverters,
                      WorkflowEntity workflow,
                      Run run,
                      int runNodeLimit,
                      TaskCompletionSource<Run>? completionSource)
    {
        ServiceScope = serviceScope;
        RepositoryService = repositoryService;
        ProgressService = progressService;
        ToolMethodRegistry = toolMethodRegistry;
        SchemaTypeRegistry = schemaTypeRegistry;
        ScriptOptionsService = scriptOptionsService;
        JsonConverters = jsonConverters;
        Workflow = workflow;
        Run = run;
        _runNodeLimit = runNodeLimit;
        CompletionSource = completionSource;

        foreach (var node in workflow.Nodes)
        {
            foreach (var connector in node.Outputs)
                _ouputConnectorToNode.Add(connector.Id, node);

            foreach (var connector in node.Inputs)
                _inputConnectorToNode.Add(connector.Id, node);
        }

        _fromToConnection = workflow.Connections.ToDictionary(c => c.From, c => c);
    }

    public int UpdateThreadCount(int delta)
    {
        return Interlocked.Add(ref _threadCount, delta);
    }

    public int GetNextThreadId()
    {
        return Interlocked.Increment(ref _threadId);
    }

    public bool TryIncrementNodesRun(out int newCount)
    {
        if (_runNodeLimit <= 0)
        {
            newCount = Interlocked.Increment(ref _nodesRun);
            return true;
        }

        while (true)
        {
            var current = Volatile.Read(ref _nodesRun);
            if (current >= _runNodeLimit)
            {
                newCount = current;
                return false;
            }

            var next = current + 1;
            if (Interlocked.CompareExchange(ref _nodesRun, next, current) == current)
            {
                newCount = next;
                return true;
            }
        }
    }

    public async Task RunUpdated()
    {
        await RepositoryService.UpsertRun(Run);
        await ProgressService.RunProgress(Run);
        if (Run.RunStatus is RunStatus.Success or RunStatus.Failed)
            CompleteRun();
    }

    private void CompleteRun()
    {
        var completionSource = Interlocked.Exchange(ref _completionSource, null);
        completionSource?.TrySetResult(Run);
    }

    public NodeEntity ResolveSingleOutput(NodeEntity node)
    {
        if (node.Outputs.Length != 1)
            throw new SharpOMaticException($"Node must have a single output but found {node.Outputs.Length}.");

        return ResolveOutput(node.Outputs[0]);
    }

    public List<NodeEntity> ResolveMultipleOutputs(NodeEntity node)
    {
        var nodes = new List<NodeEntity>();
        foreach (var connector in node.Outputs)
            nodes.Add(ResolveOutput(connector));

        return nodes;
    }

    public NodeEntity ResolveOutput(ConnectorEntity connector)
    {
        if (!_fromToConnection.TryGetValue(connector.Id, out var connection) ||
            !_inputConnectorToNode.TryGetValue(connection.To, out var nextNode))
        {
            if (string.IsNullOrWhiteSpace(connector.Name))
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

    public ContextObject TypedDeserialization(string json)
    {
        return JsonSerializer.Deserialize<ContextObject>(json, new JsonSerializerOptions().BuildOptions(JsonConverters))!;
    }

    public void MergeContexts(ContextObject target, ContextObject source)
    {
        foreach (var key in source.Keys)
        {
            if (!target.TryGetValue(key, out var targetValue))
            {
                target[key] = source[key];
            }
            else
            {
                var sourceValue = source[key];

                if (targetValue is ContextObject targetObject && sourceValue is ContextObject sourceObject)
                {
                    MergeContexts(targetObject, sourceObject);
                }
                else if (targetValue is ContextList targetList && sourceValue is not ContextList)
                {
                    targetList.Add(sourceValue);
                }
                else
                {
                    var newList = new ContextList
                    {
                        targetValue,
                        sourceValue
                    };
                    target[key] = newList;
                }
            }
        }
    }
}
