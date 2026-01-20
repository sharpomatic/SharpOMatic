namespace SharpOMatic.Tests.Workflows;

public sealed class WorkflowBuilder
{
    private Guid _workflowId = Guid.NewGuid();
    private string _name = "Test Workflow";
    private string _description = "Generated for tests.";
    private readonly List<NodeEntity> _nodes = [];
    private readonly List<ConnectionEntity> _connections = [];

    public WorkflowBuilder WithId(Guid id)
    {
        _workflowId = id;
        return this;
    }

    public WorkflowBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public WorkflowBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public WorkflowBuilder AddStart(string title = "start", 
                                    bool applyInitialization = false, 
                                    params ContextEntryEntity[] entries)
    {
        var node = new StartNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.Start,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [],
            Outputs = [CreateConnector()],
            ApplyInitialization = applyInitialization,
            Initializing = CreateContextEntryList(entries)
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddEnd(string title = "end", params ContextEntryEntity[] entries)
    {
        var node = new EndNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.End,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = [],
            ApplyMappings = false,
            Mappings = CreateContextEntryList(entries)
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddCode(string title = "code", string code = "")
    {
        var node = new CodeNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.Code,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = [CreateConnector()],
            Code = code
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddEdit(string title = "edit", params ContextEntryEntity[] entries)
    {
        var node = new EditNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.Edit,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = [CreateConnector()],
            Edits = CreateContextEntryList(entries)
        };

        _nodes.Add(node);
        return this;
    }

    public record class SwitchChoice(string Name, string Code);

    public WorkflowBuilder AddSwitch(string title = "switch", params SwitchChoice[] switchChoices)
    {
        var choices = switchChoices ?? [];
        if (choices.Length < 1)
            throw new ArgumentException("AddSwitch must have at least one switch choice");

        var switches = new SwitchEntryEntity[choices.Length];
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            switches[i] = new SwitchEntryEntity
            {
                Id = Guid.NewGuid(),
                Version = 1,
                Name = choice.Name ?? string.Empty,
                Code = choice.Code ?? string.Empty
            };
        }

        var node = new SwitchNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.Switch,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = CreateConnectors([.. choices.Select(c => c.Name)]),
            Switches = switches
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddFanIn(string title = "fanin")
    {
        var node = new FanInNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.FanIn,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = [CreateConnector()],
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddFanOut(string title = "fanout", IEnumerable<string>? outputNames = null)
    {
        var names = outputNames is null ? [] : outputNames.ToArray();
        if (names.Length < 2)
            throw new ArgumentException("AddFanOut must have at least one output name");

        var node = new FanOutNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.FanOut,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = CreateConnectors(names),
            Names = names
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddModelCall(string title = "modelcall")
    {
        var node = new ModelCallNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.ModelCall,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = [CreateConnector()],
            ModelId = null,
            Instructions = string.Empty,
            Prompt = string.Empty,
            ChatInputPath = string.Empty,
            ChatOutputPath = string.Empty,
            TextOutputPath = "output.text",
            ImageInputPath = string.Empty,
            ImageOutputPath = "output.image",
            ParameterValues = []
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddBatch(string title = "batch", int batchSize = 10, int parallelBatches = 3, string inputPath = "")
    {
        var node = new BatchNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.Batch,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = CreateConnectors("continue", "process"),
            InputArrayPath = inputPath,
            BatchSize = batchSize,
            ParallelBatches = parallelBatches
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddGosub(string title = "gosub", Guid? workflowId = null)
    {
        var node = new GosubNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.Gosub,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = [CreateConnector()],
            WorkflowId = workflowId,
            ApplyInputMappings = false,
            InputMappings = CreateContextEntryList(),
            ApplyOutputMappings = false,
            OutputMappings = CreateContextEntryList()
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder AddInput(string title = "input")
    {
        var node = new InputNodeEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.Input,
            Title = title,
            Top = 0f,
            Left = 0f,
            Width = 80f,
            Height = 80f,
            Inputs = [CreateConnector()],
            Outputs = [CreateConnector()],
        };

        _nodes.Add(node);
        return this;
    }

    public WorkflowBuilder Connect(string sourceNode, string destinationNode)
    {
        if (string.IsNullOrWhiteSpace(sourceNode))
            throw new ArgumentException("Source node must be provided.", nameof(sourceNode));
        if (string.IsNullOrWhiteSpace(destinationNode))
            throw new ArgumentException("Destination node must be provided.", nameof(destinationNode));

        var (sourceTitle, outputName) = ParseSourceNode(sourceNode);
        var source = GetNodeByTitle(sourceTitle);
        var destination = GetNodeByTitle(destinationNode);

        if (destination is StartNodeEntity || destination.NodeType == NodeType.Start)
            throw new InvalidOperationException("Cannot connect to a start node.");

        var fromConnector = ResolveOutputConnector(source, outputName, sourceNode);
        var toConnector = ResolveSingleInputConnector(destination);

        _connections.Add(new ConnectionEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            From = fromConnector.Id,
            To = toConnector.Id
        });

        return this;
    }

    public WorkflowEntity Build()
    {
        return new WorkflowEntity
        {
            Id = _workflowId,
            Version = 1,
            Name = _name,
            Description = _description,
            Nodes = _nodes.ToArray(),
            Connections = _connections.ToArray()
        };
    }

    public static ContextEntryListEntity CreateContextEntryList(params ContextEntryEntity[] entries)
    {
        return new ContextEntryListEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            Entries = entries ?? Array.Empty<ContextEntryEntity>()
        };
    }

    public static ContextEntryEntity CreateBoolInput(string inputPath, bool optional = false, bool entryValue = false)
    {
        return CreateInputEntry(inputPath, optional, ContextEntryType.Bool, entryValue.ToString());
    }

    public static ContextEntryEntity CreateIntInput(string inputPath, bool optional = false, int entryValue = 0)
    {
        return CreateInputEntry(inputPath, optional, ContextEntryType.Int, entryValue.ToString());
    }

    public static ContextEntryEntity CreateDoubleInput(string inputPath, bool optional = false, double entryValue = 0)
    {
        return CreateInputEntry(inputPath, optional, ContextEntryType.Double, entryValue.ToString());
    }

    public static ContextEntryEntity CreateStringInput(string inputPath, bool optional = false, string entryValue = "")
    {
        return CreateInputEntry(inputPath, optional, ContextEntryType.String, entryValue);
    }

    public static ContextEntryEntity CreateJsonInput(string inputPath, bool optional = false, string json = "")
    {
        return CreateInputEntry(inputPath, optional, ContextEntryType.JSON, json);
    }

    public static ContextEntryEntity CreateExpressionInput(string inputPath, bool optional = false, string code = "")
    {
        return CreateInputEntry(inputPath, optional, ContextEntryType.Expression, code);
    }

    public static ContextEntryEntity CreateBoolUpsert(string inputPath, bool entryValue = false)
    {
        return CreateUpsertEntry(inputPath, ContextEntryType.Bool, entryValue.ToString());
    }

    public static ContextEntryEntity CreateIntUpsert(string inputPath, int entryValue = 0)
    {
        return CreateUpsertEntry(inputPath, ContextEntryType.Int, entryValue.ToString());
    }

    public static ContextEntryEntity CreateDoubleUpsert(string inputPath, double entryValue = 0)
    {
        return CreateUpsertEntry(inputPath, ContextEntryType.Double, entryValue.ToString());
    }

    public static ContextEntryEntity CreateStringUpsert(string inputPath, string entryValue = "")
    {
        return CreateUpsertEntry(inputPath, ContextEntryType.String, entryValue);
    }

    public static ContextEntryEntity CreateJsonUpsert(string inputPath, string json = "")
    {
        return CreateUpsertEntry(inputPath, ContextEntryType.JSON, json);
    }

    public static ContextEntryEntity CreateExpressionUpsert(string inputPath, string code = "")
    {
        return CreateUpsertEntry(inputPath, ContextEntryType.Expression, code);
    }

    public static ContextEntryEntity CreateUpsertEntry(string inputPath, ContextEntryType entryType, string entryValue, bool optional = false)
    {
        return CreateEntry(ContextEntryPurpose.Upsert, inputPath, optional, entryType, entryValue);
    }

    public static ContextEntryEntity CreateDeleteEntry(string inputPath)
    {
        return CreateEntry(ContextEntryPurpose.Delete, inputPath, optional: false, ContextEntryType.String, entryValue: string.Empty);
    }

    public static ContextEntryEntity CreateOutputEntry(string inputPath, string outputPath)
    {
        return CreateEntry(ContextEntryPurpose.Output, inputPath, optional: false, ContextEntryType.String, entryValue: string.Empty, outputPath: outputPath);
    }

    public static ContextEntryEntity CreateInputEntry(string inputPath, bool optional, ContextEntryType entryType, string entryValue)
    {
        return CreateEntry(ContextEntryPurpose.Input, inputPath, optional, entryType, entryValue);
    }

    private static ContextEntryEntity CreateEntry(
        ContextEntryPurpose purpose,
        string inputPath,
        bool optional,
        ContextEntryType entryType,
        string entryValue,
        string outputPath = "")
    {
        return new ContextEntryEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            Purpose = purpose,
            InputPath = inputPath,
            OutputPath = outputPath,
            Optional = optional,
            EntryType = entryType,
            EntryValue = entryValue
        };
    }

    public NodeEntity GetNodeByTitle(string title)
    {
        var node = _nodes.FirstOrDefault(n => string.Equals(n.Title, title, StringComparison.Ordinal));
        if (node is null)
            throw new InvalidOperationException($"Node with title '{title}' was not found.");

        return node;
    }

    private static (string SourceTitle, string? OutputName) ParseSourceNode(string sourceNode)
    {
        var dotIndex = sourceNode.LastIndexOf('.');
        if (dotIndex < 0)
            return (sourceNode, null);

        if (dotIndex == 0 || dotIndex == sourceNode.Length - 1)
            throw new ArgumentException("Source node must be in 'node.output' format when using dot notation.", nameof(sourceNode));

        var sourceTitle = sourceNode[..dotIndex];
        var outputName = sourceNode[(dotIndex + 1)..];

        if (string.IsNullOrWhiteSpace(sourceTitle) || string.IsNullOrWhiteSpace(outputName))
            throw new ArgumentException("Source node must be in 'node.output' format when using dot notation.", nameof(sourceNode));

        return (sourceTitle, outputName);
    }

    private static ConnectorEntity ResolveOutputConnector(NodeEntity node, string? outputName, string sourceNode)
    {
        if (outputName is null)
        {
            if (node.Outputs.Length != 1)
                throw new InvalidOperationException($"Node '{node.Title}' must have exactly one output when using '{sourceNode}'.");

            return node.Outputs[0];
        }

        if (node.Outputs.Length == 0)
            throw new InvalidOperationException($"Node '{node.Title}' has no outputs to match '{outputName}'.");

        var connector = node.Outputs.FirstOrDefault(output => string.Equals(output.Name, outputName, StringComparison.Ordinal));
        if (connector is null)
            throw new InvalidOperationException($"Node '{node.Title}' does not have an output named '{outputName}'.");

        return connector;
    }

    private static ConnectorEntity ResolveSingleInputConnector(NodeEntity node)
    {
        if (node.Inputs.Length != 1)
            throw new InvalidOperationException($"Node '{node.Title}' must have exactly one input but has {node.Inputs.Length}.");

        return node.Inputs[0];
    }

    private static ConnectorEntity CreateConnector(string name = "")
    {
        return new ConnectorEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            Name = name ?? string.Empty
        };
    }

    private static ConnectorEntity[] CreateConnectors(params string[] names)
    {
        if (names is null || names.Length == 0)
        {
            return Array.Empty<ConnectorEntity>();
        }

        var connectors = new ConnectorEntity[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            connectors[i] = CreateConnector(names[i] ?? string.Empty);
        }

        return connectors;
    }

}
