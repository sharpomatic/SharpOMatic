namespace SharpOMatic.Engine.Contexts;

public class ProcessContext : ExecutionContext
{
    private readonly ConcurrentDictionary<ExecutionContext, byte> _activeContexts = new();
    private readonly ConcurrentDictionary<int, ThreadContext> _threads = new();
    private readonly ConcurrentDictionary<Guid, string> _pinnedWorkflowSnapshots = new();
    private TaskCompletionSource<Run>? _completionSource;

    private int _threadId = 1;
    private int _threadCount = 1;
    private int _nodesRun = 0;
    private int _runNodeLimit = 0;
    private int _nextStreamSequence = 0;
    private int _streamSequenceInitialized = 0;

    public IServiceScope ServiceScope { get; }
    public IRepositoryService RepositoryService { get; }
    public IAssetStore AssetStore { get; }
    public IEnumerable<IProgressService> ProgressServices { get; }
    public IToolMethodRegistry ToolMethodRegistry { get; }
    public ISchemaTypeRegistry SchemaTypeRegistry { get; }
    public IScriptOptionsService ScriptOptionsService { get; }
    public IEnumerable<JsonConverter> JsonConverters { get; }
    public Run Run { get; }
    public Conversation? Conversation { get; }
    public ConversationCheckpoint? Checkpoint { get; }
    public NodeResumeInput? ConversationResumeInput { get; }
    public int? ConversationTurnNumber { get; }
    public string? ConversationLeaseOwner { get; }
    public string? StreamConversationId { get; }
    public PendingConversationSuspend? PendingConversationSuspend { get; private set; }
    public TaskCompletionSource<Run>? CompletionSource { get; }
    public int ActiveThreadCount => _threadCount;
    public int RunNodeLimit => _runNodeLimit;
    public int NodesRun => Volatile.Read(ref _nodesRun);
    public IReadOnlyCollection<ExecutionContext> ActiveContexts => _activeContexts.Keys.ToList();
    public bool HasPendingSuspend => PendingConversationSuspend is not null;

    public ProcessContext(
        IServiceScope serviceScope,
        Run run,
        int runNodeLimit,
        TaskCompletionSource<Run>? completionSource,
        Conversation? conversation = null,
        ConversationCheckpoint? checkpoint = null,
        NodeResumeInput? conversationResumeInput = null,
        int? conversationTurnNumber = null,
        string? conversationLeaseOwner = null,
        string? streamConversationId = null,
        IEnumerable<ConversationWorkflowSnapshot>? pinnedWorkflowSnapshots = null
    )
        : base(null)
    {
        ServiceScope = serviceScope;
        Run = run;
        Conversation = conversation;
        Checkpoint = checkpoint;
        ConversationResumeInput = conversationResumeInput;
        ConversationTurnNumber = conversationTurnNumber;
        ConversationLeaseOwner = conversationLeaseOwner;
        StreamConversationId = NormalizeConversationId(streamConversationId);
        RepositoryService = serviceScope.ServiceProvider.GetRequiredService<IRepositoryService>();
        AssetStore = serviceScope.ServiceProvider.GetRequiredService<IAssetStore>();
        ProgressServices = serviceScope.ServiceProvider.GetRequiredService<IEnumerable<IProgressService>>();
        ToolMethodRegistry = serviceScope.ServiceProvider.GetRequiredService<IToolMethodRegistry>();
        SchemaTypeRegistry = serviceScope.ServiceProvider.GetRequiredService<ISchemaTypeRegistry>();
        ScriptOptionsService = serviceScope.ServiceProvider.GetRequiredService<IScriptOptionsService>();
        JsonConverters = serviceScope.ServiceProvider.GetRequiredService<IJsonConverterService>().GetConverters();
        _runNodeLimit = runNodeLimit;
        CompletionSource = completionSource;
        _completionSource = completionSource;

        if (pinnedWorkflowSnapshots is not null)
        {
            foreach (var snapshot in pinnedWorkflowSnapshots)
                _pinnedWorkflowSnapshots[snapshot.WorkflowId] = snapshot.WorkflowJson;
        }

        TrackContext(this);
    }

    public ThreadContext CreateThread(ContextObject nodeContext, ExecutionContext currentContext, bool incrementGosubThreads = true)
    {
        var threadContext = new ThreadContext(this, currentContext, nodeContext);
        _threads.TryAdd(threadContext.ThreadId, threadContext);
        if (incrementGosubThreads)
        {
            var gosubContext = GosubContext.Find(currentContext);
            gosubContext?.IncrementThreads();
        }
        return threadContext;
    }

    public ThreadContext RestoreThread(ContextObject nodeContext, ExecutionContext currentContext)
    {
        return CreateThread(nodeContext, currentContext, incrementGosubThreads: false);
    }

    public void RemoveThread(ThreadContext threadContext)
    {
        _threads.TryRemove(threadContext.ThreadId, out _);
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
        foreach (var progressService in ProgressServices)
            await progressService.RunProgress(Run);
        if (Run.RunStatus is RunStatus.Success or RunStatus.Failed or RunStatus.Suspended)
            CompleteRun();
    }

    public void InitializeStreamSequence(int nextSequence)
    {
        var normalized = Math.Max(1, nextSequence);
        Volatile.Write(ref _nextStreamSequence, normalized - 1);
        Volatile.Write(ref _streamSequenceInitialized, 1);
    }

    public int AllocateNextStreamSequence()
    {
        if (Volatile.Read(ref _streamSequenceInitialized) == 0)
            throw new SharpOMaticException("Stream event sequence has not been initialized.");

        return Interlocked.Increment(ref _nextStreamSequence);
    }

    public int PeekNextStreamSequence()
    {
        if (Volatile.Read(ref _streamSequenceInitialized) == 0)
            throw new SharpOMaticException("Stream event sequence has not been initialized.");

        return Volatile.Read(ref _nextStreamSequence) + 1;
    }

    public async Task<List<StreamEvent>> AppendStreamEvents(IEnumerable<StreamEventWrite> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var writes = events.ToList();
        if (writes.Count == 0)
            return [];

        var created = DateTime.UtcNow;
        var streamEvents = new List<StreamEvent>(writes.Count);
        var progressEvents = new List<StreamEventProgressItem>(writes.Count);

        foreach (var write in writes)
        {
            ValidateStreamEventWrite(write);

            var streamEvent = new StreamEvent()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = Run.RunId,
                WorkflowId = Run.WorkflowId,
                ConversationId = StreamConversationId,
                SequenceNumber = AllocateNextStreamSequence(),
                Created = created,
                EventKind = write.EventKind,
                MessageId = write.MessageId,
                MessageRole = write.MessageRole,
                ActivityType = write.ActivityType,
                Replace = write.Replace,
                TextDelta = write.TextDelta,
                ToolCallId = write.ToolCallId,
                ParentMessageId = write.ParentMessageId,
                Metadata = write.Metadata,
                HideFromReply = false,
            };

            streamEvents.Add(streamEvent);
            progressEvents.Add(new StreamEventProgressItem() { Event = streamEvent, Silent = write.Silent });
        }

        await RepositoryService.AppendStreamEvents(streamEvents);

        foreach (var progressService in ProgressServices)
            await progressService.StreamEventProgress(Run, progressEvents);

        return streamEvents;
    }

    public void PinWorkflowSnapshot(WorkflowEntity workflow)
    {
        _pinnedWorkflowSnapshots[workflow.Id] = WorkflowSnapshotSerializer.SerializeWorkflow(workflow);
    }

    public async Task<WorkflowEntity> GetOrCreatePinnedWorkflow(Guid workflowId)
    {
        if (_pinnedWorkflowSnapshots.TryGetValue(workflowId, out var workflowJson))
            return WorkflowSnapshotSerializer.DeserializeWorkflow(workflowJson);

        var workflow = await RepositoryService.GetWorkflow(workflowId);
        PinWorkflowSnapshot(workflow);
        return workflow;
    }

    public IReadOnlyList<ConversationWorkflowSnapshot> GetPinnedWorkflowSnapshots()
    {
        return _pinnedWorkflowSnapshots
            .Select(item => new ConversationWorkflowSnapshot() { WorkflowId = item.Key, WorkflowJson = item.Value })
            .OrderBy(item => item.WorkflowId)
            .ToList();
    }

    public void RequestConversationSuspend(ThreadContext threadContext, NodeEntity node, NodeExecutionResult result)
    {
        if (Conversation is null)
            throw new SharpOMaticException("Node suspension requires an active conversation.");

        if (PendingConversationSuspend is not null)
            throw new SharpOMaticException("Only one node can be waiting for conversation resume at a time.");

        if (node is not SuspendNodeEntity && node is not FrontendToolCallNodeEntity)
            throw new SharpOMaticException("Only suspend and Frontend Tool Call nodes can wait for resume.");

        if (result.NextNodes.Count > 0)
            throw new SharpOMaticException("A suspended node cannot continue to downstream nodes.");

        if (!IsSupportedConversationSuspendScope(threadContext.CurrentContext))
            throw new SharpOMaticException("Conversation suspension is only supported in the root workflow and gosub scopes.");

        PendingConversationSuspend = new PendingConversationSuspend()
        {
            ResumeNodeId = node.Id,
            ContextJson = threadContext.NodeContext.Serialize(JsonConverters),
            ResumeStateJson = result.ResumeStateJson,
            WorkflowSnapshotsJson = JsonSerializer.Serialize(GetPinnedWorkflowSnapshots()),
            GosubStackJson = JsonSerializer.Serialize(BuildConversationGosubFrames(threadContext)),
            Message = result.Message,
        };
    }

    private bool IsSupportedConversationSuspendScope(ExecutionContext currentContext)
    {
        var scope = currentContext;
        while (scope is not null)
        {
            if (scope is BatchContext or FanOutInContext)
                return false;

            scope = scope.Parent!;
        }

        return true;
    }

    private List<ConversationGosubFrame> BuildConversationGosubFrames(ThreadContext threadContext)
    {
        var frames = new List<ConversationGosubFrame>();
        var scope = threadContext.CurrentContext;

        while (scope is not null)
        {
            if (scope is WorkflowContext workflowContext && scope.Parent is GosubContext gosubContext)
            {
                frames.Add(
                    new ConversationGosubFrame()
                    {
                        ChildWorkflowId = workflowContext.WorkflowId,
                        ParentContextJson = gosubContext.ParentContext.Serialize(JsonConverters),
                        ReturnNodeId = gosubContext.ReturnNode?.Id,
                        ParentTraceId = gosubContext.ParentTraceId,
                        ApplyOutputMappings = gosubContext.ApplyOutputMappings,
                        OutputMappingsJson = JsonSerializer.Serialize(gosubContext.OutputMappings),
                    }
                );
            }

            scope = scope.Parent;
        }

        frames.Reverse();
        return frames;
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

                if (targetValue is ContextList targetList1 && sourceValue is ContextList sourceList)
                {
                    targetList1.AddRange(sourceList);
                }
                else if (targetValue is ContextList targetList2 && sourceValue is not ContextList)
                {
                    targetList2.Add(sourceValue);
                }
                else if (targetValue is not ContextList && sourceValue is ContextList targetList3)
                {
                    var newList = new ContextList { targetValue };

                    newList.AddRange(targetList3);
                    target[key] = newList;
                }
                else
                {
                    var newList = new ContextList { targetValue, sourceValue };
                    target[key] = newList;
                }
            }
        }
    }

    public void MergeOutputValue(ContextObject target, string outputPath, object? sourceValue)
    {
        if (!target.TryGet<object?>(outputPath, out var targetValue))
        {
            if (!target.TrySet(outputPath, sourceValue))
                throw new SharpOMaticException($"Could not set '{outputPath}' into context.");

            return;
        }

        if (targetValue is ContextList targetList1 && sourceValue is ContextList sourceList)
        {
            targetList1.AddRange(sourceList);
        }
        else if (targetValue is ContextList targetList2 && sourceValue is not ContextList)
        {
            targetList2.Add(sourceValue);
        }
        else if (targetValue is not ContextList && sourceValue is ContextList sourceList2)
        {
            var newList = new ContextList { targetValue };
            newList.AddRange(sourceList2);
            if (!target.TrySet(outputPath, newList))
                throw new SharpOMaticException($"Could not set '{outputPath}' into context.");
        }
        else
        {
            var newList = new ContextList { targetValue, sourceValue };
            if (!target.TrySet(outputPath, newList))
                throw new SharpOMaticException($"Could not set '{outputPath}' into context.");
        }
    }

    internal void TrackContext(ExecutionContext context)
    {
        _activeContexts.TryAdd(context, 0);
    }

    internal void UntrackContext(ExecutionContext context)
    {
        _activeContexts.TryRemove(context, out _);
    }

    private void CompleteRun()
    {
        var completionSource = Interlocked.Exchange(ref _completionSource, null);
        completionSource?.TrySetResult(Run);
    }

    private static void ValidateStreamEventWrite(StreamEventWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);

        switch (write.EventKind)
        {
            case StreamEventKind.TextStart:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("TextStart stream events require a MessageId.");

                if (!write.MessageRole.HasValue)
                    throw new SharpOMaticException("TextStart stream events require a MessageRole.");

                if (write.MessageRole == StreamMessageRole.Reasoning)
                    throw new SharpOMaticException("TextStart stream events cannot use the Reasoning role.");

                if (!string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("TextStart stream events cannot include TextDelta.");
                break;
            case StreamEventKind.TextContent:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("TextContent stream events require a MessageId.");

                if (string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("TextContent stream events require a non-empty TextDelta.");
                break;
            case StreamEventKind.TextEnd:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("TextEnd stream events require a MessageId.");

                if (!string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("TextEnd stream events cannot include TextDelta.");
                break;
            case StreamEventKind.ReasoningStart:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ReasoningStart stream events require a MessageId.");

                if (write.MessageRole.HasValue)
                    throw new SharpOMaticException("ReasoningStart stream events cannot include a MessageRole.");

                if (!string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("ReasoningStart stream events cannot include TextDelta.");
                break;
            case StreamEventKind.ReasoningMessageStart:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ReasoningMessageStart stream events require a MessageId.");

                if (write.MessageRole != StreamMessageRole.Reasoning)
                    throw new SharpOMaticException("ReasoningMessageStart stream events require the Reasoning role.");

                if (!string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("ReasoningMessageStart stream events cannot include TextDelta.");
                break;
            case StreamEventKind.ReasoningMessageContent:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ReasoningMessageContent stream events require a MessageId.");

                if (string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("ReasoningMessageContent stream events require a non-empty TextDelta.");
                break;
            case StreamEventKind.ReasoningMessageEnd:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ReasoningMessageEnd stream events require a MessageId.");

                if (!string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("ReasoningMessageEnd stream events cannot include TextDelta.");
                break;
            case StreamEventKind.ReasoningEnd:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ReasoningEnd stream events require a MessageId.");

                if (!string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("ReasoningEnd stream events cannot include TextDelta.");
                break;
            case StreamEventKind.ToolCallStart:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ToolCallStart stream events require a MessageId.");

                if (string.IsNullOrWhiteSpace(write.ToolCallId))
                    throw new SharpOMaticException("ToolCallStart stream events require a ToolCallId.");

                if (write.MessageRole.HasValue)
                    throw new SharpOMaticException("ToolCallStart stream events cannot include a MessageRole.");

                if (string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("ToolCallStart stream events require a non-empty TextDelta for the tool name.");
                break;
            case StreamEventKind.ToolCallArgs:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ToolCallArgs stream events require a MessageId.");

                if (string.IsNullOrWhiteSpace(write.ToolCallId))
                    throw new SharpOMaticException("ToolCallArgs stream events require a ToolCallId.");

                if (string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("ToolCallArgs stream events require a non-empty TextDelta.");

                if (write.MessageRole.HasValue)
                    throw new SharpOMaticException("ToolCallArgs stream events cannot include a MessageRole.");

                if (!string.IsNullOrWhiteSpace(write.ParentMessageId))
                    throw new SharpOMaticException("ToolCallArgs stream events cannot include ParentMessageId.");
                break;
            case StreamEventKind.ToolCallEnd:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ToolCallEnd stream events require a MessageId.");

                if (string.IsNullOrWhiteSpace(write.ToolCallId))
                    throw new SharpOMaticException("ToolCallEnd stream events require a ToolCallId.");

                if (write.MessageRole.HasValue)
                    throw new SharpOMaticException("ToolCallEnd stream events cannot include a MessageRole.");

                if (!string.IsNullOrWhiteSpace(write.TextDelta))
                    throw new SharpOMaticException("ToolCallEnd stream events cannot include TextDelta.");

                if (!string.IsNullOrWhiteSpace(write.ParentMessageId))
                    throw new SharpOMaticException("ToolCallEnd stream events cannot include ParentMessageId.");
                break;
            case StreamEventKind.ToolCallResult:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ToolCallResult stream events require a MessageId.");

                if (string.IsNullOrWhiteSpace(write.ToolCallId))
                    throw new SharpOMaticException("ToolCallResult stream events require a ToolCallId.");

                if (write.MessageRole.HasValue)
                    throw new SharpOMaticException("ToolCallResult stream events cannot include a MessageRole.");

                if (!string.IsNullOrWhiteSpace(write.ParentMessageId))
                    throw new SharpOMaticException("ToolCallResult stream events cannot include ParentMessageId.");

                if (write.TextDelta is null)
                    throw new SharpOMaticException("ToolCallResult stream events require TextDelta.");
                break;
            case StreamEventKind.ActivitySnapshot:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ActivitySnapshot stream events require a MessageId.");

                if (string.IsNullOrWhiteSpace(write.ActivityType))
                    throw new SharpOMaticException("ActivitySnapshot stream events require an ActivityType.");

                if (write.MessageRole.HasValue)
                    throw new SharpOMaticException("ActivitySnapshot stream events cannot include a MessageRole.");

                if (!string.IsNullOrWhiteSpace(write.ToolCallId))
                    throw new SharpOMaticException("ActivitySnapshot stream events cannot include ToolCallId.");

                if (!string.IsNullOrWhiteSpace(write.ParentMessageId))
                    throw new SharpOMaticException("ActivitySnapshot stream events cannot include ParentMessageId.");

                ValidateActivitySnapshotContent(write.TextDelta);
                break;
            case StreamEventKind.ActivityDelta:
                if (string.IsNullOrWhiteSpace(write.MessageId))
                    throw new SharpOMaticException("ActivityDelta stream events require a MessageId.");

                if (string.IsNullOrWhiteSpace(write.ActivityType))
                    throw new SharpOMaticException("ActivityDelta stream events require an ActivityType.");

                if (write.Replace.HasValue)
                    throw new SharpOMaticException("ActivityDelta stream events cannot include Replace.");

                if (write.MessageRole.HasValue)
                    throw new SharpOMaticException("ActivityDelta stream events cannot include a MessageRole.");

                if (!string.IsNullOrWhiteSpace(write.ToolCallId))
                    throw new SharpOMaticException("ActivityDelta stream events cannot include ToolCallId.");

                if (!string.IsNullOrWhiteSpace(write.ParentMessageId))
                    throw new SharpOMaticException("ActivityDelta stream events cannot include ParentMessageId.");

                ValidateActivityDeltaPatch(write.TextDelta);
                break;
            case StreamEventKind.StepStart:
                ValidateStepEventWrite(write, "StepStart");
                break;
            case StreamEventKind.StepEnd:
                ValidateStepEventWrite(write, "StepEnd");
                break;
            case StreamEventKind.StateSnapshot:
                ValidateStateSnapshotEventWrite(write);
                break;
            case StreamEventKind.StateDelta:
                ValidateStateDeltaEventWrite(write);
                break;
            default:
                throw new SharpOMaticException($"Unsupported stream event kind '{write.EventKind}'.");
        }
    }

    private static void ValidateStepEventWrite(StreamEventWrite write, string eventKindName)
    {
        if (!string.IsNullOrWhiteSpace(write.MessageId))
            throw new SharpOMaticException($"{eventKindName} stream events cannot include a MessageId.");

        if (write.MessageRole.HasValue)
            throw new SharpOMaticException($"{eventKindName} stream events cannot include a MessageRole.");

        if (!string.IsNullOrWhiteSpace(write.ActivityType))
            throw new SharpOMaticException($"{eventKindName} stream events cannot include ActivityType.");

        if (write.Replace.HasValue)
            throw new SharpOMaticException($"{eventKindName} stream events cannot include Replace.");

        if (!string.IsNullOrWhiteSpace(write.ToolCallId))
            throw new SharpOMaticException($"{eventKindName} stream events cannot include ToolCallId.");

        if (!string.IsNullOrWhiteSpace(write.ParentMessageId))
            throw new SharpOMaticException($"{eventKindName} stream events cannot include ParentMessageId.");

        if (string.IsNullOrWhiteSpace(write.TextDelta))
            throw new SharpOMaticException($"{eventKindName} stream events require TextDelta to contain the step name.");
    }

    private static void ValidateActivitySnapshotContent(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
            throw new SharpOMaticException("ActivitySnapshot stream events require TextDelta.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(contentJson);
        }
        catch (JsonException)
        {
            throw new SharpOMaticException("ActivitySnapshot stream events require TextDelta to contain valid JSON.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new SharpOMaticException("ActivitySnapshot stream events require TextDelta to contain a JSON object.");
        }
    }

    private static void ValidateActivityDeltaPatch(string? patchJson)
    {
        ValidateJsonPatchPayload(patchJson, "ActivityDelta");
    }

    private static void ValidateStateSnapshotEventWrite(StreamEventWrite write)
    {
        if (!string.IsNullOrWhiteSpace(write.MessageId))
            throw new SharpOMaticException("StateSnapshot stream events cannot include a MessageId.");

        if (write.MessageRole.HasValue)
            throw new SharpOMaticException("StateSnapshot stream events cannot include a MessageRole.");

        if (!string.IsNullOrWhiteSpace(write.ActivityType))
            throw new SharpOMaticException("StateSnapshot stream events cannot include ActivityType.");

        if (write.Replace.HasValue)
            throw new SharpOMaticException("StateSnapshot stream events cannot include Replace.");

        if (!string.IsNullOrWhiteSpace(write.ToolCallId))
            throw new SharpOMaticException("StateSnapshot stream events cannot include ToolCallId.");

        if (!string.IsNullOrWhiteSpace(write.ParentMessageId))
            throw new SharpOMaticException("StateSnapshot stream events cannot include ParentMessageId.");

        ValidateStateSnapshotContent(write.TextDelta);
    }

    private static void ValidateStateDeltaEventWrite(StreamEventWrite write)
    {
        if (!string.IsNullOrWhiteSpace(write.MessageId))
            throw new SharpOMaticException("StateDelta stream events cannot include a MessageId.");

        if (write.MessageRole.HasValue)
            throw new SharpOMaticException("StateDelta stream events cannot include a MessageRole.");

        if (!string.IsNullOrWhiteSpace(write.ActivityType))
            throw new SharpOMaticException("StateDelta stream events cannot include ActivityType.");

        if (write.Replace.HasValue)
            throw new SharpOMaticException("StateDelta stream events cannot include Replace.");

        if (!string.IsNullOrWhiteSpace(write.ToolCallId))
            throw new SharpOMaticException("StateDelta stream events cannot include ToolCallId.");

        if (!string.IsNullOrWhiteSpace(write.ParentMessageId))
            throw new SharpOMaticException("StateDelta stream events cannot include ParentMessageId.");

        ValidateStateDeltaPatch(write.TextDelta);
    }

    private static void ValidateStateSnapshotContent(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
            throw new SharpOMaticException("StateSnapshot stream events require TextDelta.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(contentJson);
        }
        catch (JsonException)
        {
            throw new SharpOMaticException("StateSnapshot stream events require TextDelta to contain valid JSON.");
        }

        using (document)
        {
        }
    }

    private static void ValidateStateDeltaPatch(string? patchJson)
    {
        ValidateJsonPatchPayload(patchJson, "StateDelta");
    }

    private static void ValidateJsonPatchPayload(string? patchJson, string eventKindName)
    {
        if (string.IsNullOrWhiteSpace(patchJson))
            throw new SharpOMaticException($"{eventKindName} stream events require TextDelta.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(patchJson);
        }
        catch (JsonException)
        {
            throw new SharpOMaticException($"{eventKindName} stream events require TextDelta to contain valid JSON.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new SharpOMaticException($"{eventKindName} stream events require TextDelta to contain a JSON array.");

            var operations = document.RootElement.EnumerateArray().ToList();
            if (operations.Count == 0)
                throw new SharpOMaticException($"{eventKindName} stream events require TextDelta to contain at least one JSON Patch operation.");

            for (var i = 0; i < operations.Count; i += 1)
                ValidateJsonPatchOperation(operations[i], i, eventKindName);
        }
    }

    private static void ValidateJsonPatchOperation(JsonElement operation, int index, string eventKindName)
    {
        if (operation.ValueKind != JsonValueKind.Object)
            throw new SharpOMaticException($"{eventKindName} JSON Patch operation at index {index} must be an object.");

        if (!TryGetRequiredStringProperty(operation, "op", out var op))
            throw new SharpOMaticException($"{eventKindName} JSON Patch operation at index {index} must include a non-empty 'op' property.");

        if (!TryGetRequiredStringProperty(operation, "path", out _))
            throw new SharpOMaticException($"{eventKindName} JSON Patch operation at index {index} must include a non-empty 'path' property.");

        if (!IsSupportedPatchOperation(op))
            throw new SharpOMaticException($"{eventKindName} JSON Patch operation at index {index} has unsupported op '{op}'.");

        if ((op == "move" || op == "copy") && !TryGetRequiredStringProperty(operation, "from", out _))
            throw new SharpOMaticException($"{eventKindName} JSON Patch operation '{op}' at index {index} must include a non-empty 'from' property.");

        if ((op == "add" || op == "replace" || op == "test") && !operation.TryGetProperty("value", out _))
            throw new SharpOMaticException($"{eventKindName} JSON Patch operation '{op}' at index {index} must include a 'value' property.");
    }

    private static bool TryGetRequiredStringProperty(JsonElement element, string propertyName, [NotNullWhen(true)] out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var propertyValue) || propertyValue.ValueKind != JsonValueKind.String)
            return false;

        value = propertyValue.GetString()?.Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsSupportedPatchOperation(string op)
    {
        return op is "add" or "remove" or "replace" or "move" or "copy" or "test";
    }

    private static string? NormalizeConversationId(string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return null;

        if (conversationId.Length > 256)
            throw new SharpOMaticException("Stream conversation id cannot be longer than 256 characters.");

        return conversationId.Trim();
    }
}
