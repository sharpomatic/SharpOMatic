namespace SharpOMatic.AGUI.Services;

public interface IAgUiRunEventBroker
{
    IAsyncEnumerable<AgUiRunUpdate> Subscribe(Guid runId, CancellationToken cancellationToken);
    void PublishRun(Run run);
    void PublishStreamEvents(Guid runId, IEnumerable<StreamEvent> events);
}

public sealed record AgUiRunUpdate(Run? Run, IReadOnlyList<StreamEvent>? StreamEvents)
{
    public bool IsTerminal => Run?.RunStatus is RunStatus.Success or RunStatus.Suspended or RunStatus.Failed;

    public static AgUiRunUpdate ForRun(Run run)
    {
        return new AgUiRunUpdate(run, null);
    }

    public static AgUiRunUpdate ForStreamEvents(IReadOnlyList<StreamEvent> events)
    {
        return new AgUiRunUpdate(null, events);
    }
}

internal sealed class AgUiRunEventBuffer
{
    private readonly object _gate = new();
    private readonly List<AgUiRunUpdate> _history = [];
    private readonly Dictionary<int, Channel<AgUiRunUpdate>> _subscribers = [];
    private int _nextSubscriberId = 0;

    public bool IsCompleted { get; private set; }
    public DateTime? CompletedUtc { get; private set; }

    public async IAsyncEnumerable<AgUiRunUpdate> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<AgUiRunUpdate>();
        int subscriberId;

        lock (_gate)
        {
            subscriberId = ++_nextSubscriberId;
            foreach (var update in _history)
                channel.Writer.TryWrite(update);

            if (IsCompleted)
                channel.Writer.TryComplete();
            else
                _subscribers[subscriberId] = channel;
        }

        try
        {
            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
                yield return update;
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(subscriberId);
            }
        }
    }

    public void Publish(AgUiRunUpdate update)
    {
        lock (_gate)
        {
            _history.Add(update);
            foreach (var subscriber in _subscribers.Values)
                subscriber.Writer.TryWrite(update);

            if (!update.IsTerminal)
                return;

            IsCompleted = true;
            CompletedUtc = DateTime.UtcNow;
            foreach (var subscriber in _subscribers.Values)
                subscriber.Writer.TryComplete();
            _subscribers.Clear();
        }
    }
}

public sealed class AgUiRunEventBroker : IAgUiRunEventBroker
{
    private static readonly TimeSpan CompletedRetention = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<Guid, AgUiRunEventBuffer> _buffers = new();

    public IAsyncEnumerable<AgUiRunUpdate> Subscribe(Guid runId, CancellationToken cancellationToken)
    {
        CleanupExpiredBuffers();
        return _buffers.GetOrAdd(runId, _ => new AgUiRunEventBuffer()).Subscribe(cancellationToken);
    }

    public void PublishRun(Run run)
    {
        ArgumentNullException.ThrowIfNull(run);

        CleanupExpiredBuffers();
        _buffers.GetOrAdd(run.RunId, _ => new AgUiRunEventBuffer()).Publish(AgUiRunUpdate.ForRun(CloneRun(run)));
    }

    public void PublishStreamEvents(Guid runId, IEnumerable<StreamEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var streamEvents = events.Select(CloneStreamEvent).ToList();
        if (streamEvents.Count == 0)
            return;

        CleanupExpiredBuffers();
        _buffers.GetOrAdd(runId, _ => new AgUiRunEventBuffer()).Publish(AgUiRunUpdate.ForStreamEvents(streamEvents));
    }

    private void CleanupExpiredBuffers()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in _buffers)
        {
            if (!entry.Value.IsCompleted || !entry.Value.CompletedUtc.HasValue)
                continue;

            if (now - entry.Value.CompletedUtc.Value < CompletedRetention)
                continue;

            _buffers.TryRemove(entry.Key, out _);
        }
    }

    private static Run CloneRun(Run run)
    {
        return new Run()
        {
            RunId = run.RunId,
            WorkflowId = run.WorkflowId,
            ConversationId = run.ConversationId,
            TurnNumber = run.TurnNumber,
            Created = run.Created,
            RunStatus = run.RunStatus,
            NeedsEditorEvents = run.NeedsEditorEvents,
            Started = run.Started,
            Stopped = run.Stopped,
            InputEntries = run.InputEntries,
            InputContext = run.InputContext,
            OutputContext = run.OutputContext,
            CustomData = run.CustomData,
            Message = run.Message,
            Error = run.Error,
        };
    }

    private static StreamEvent CloneStreamEvent(StreamEvent streamEvent)
    {
        return new StreamEvent()
        {
            StreamEventId = streamEvent.StreamEventId,
            RunId = streamEvent.RunId,
            WorkflowId = streamEvent.WorkflowId,
            ConversationId = streamEvent.ConversationId,
            SequenceNumber = streamEvent.SequenceNumber,
            Created = streamEvent.Created,
            EventKind = streamEvent.EventKind,
            MessageId = streamEvent.MessageId,
            MessageRole = streamEvent.MessageRole,
            TextDelta = streamEvent.TextDelta,
            Metadata = streamEvent.Metadata,
        };
    }
}
