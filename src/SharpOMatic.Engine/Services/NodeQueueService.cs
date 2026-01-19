using System.Collections.Concurrent;

namespace SharpOMatic.Engine.Services;

public class NodeQueueService : INodeQueueService
{
    private readonly Channel<(ThreadContext threadContext, NodeEntity node)> _queue;
    private readonly ConcurrentDictionary<Guid, byte> _blockedRuns = new();

    public NodeQueueService()
    {
        _queue = Channel.CreateUnbounded<(ThreadContext, NodeEntity)>();
    }

    public void Enqueue(ThreadContext threadContext, NodeEntity node)
    {
        _queue.Writer.TryWrite((threadContext, node));
    }

    public async ValueTask<(ThreadContext threadContext, NodeEntity node)> DequeueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var item = await _queue.Reader.ReadAsync(cancellationToken);
            if (!_blockedRuns.ContainsKey(item.threadContext.ProcessContext.Run.RunId))
            {
                if (_queue.Reader.Count == 0)
                    _blockedRuns.Clear();

                return item;
            }
        }
    }

    public void RemoveRunNodes(Guid runId)
    {
        _blockedRuns.TryAdd(runId, 0);
    }
}
