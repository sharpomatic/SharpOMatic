
namespace SharpOMatic.Engine.Services;

public class NodeQueueService : INodeQueueService
{
    private readonly Channel<NextNodeData> _queue;
    private readonly ConcurrentDictionary<Guid, byte> _blockedRuns = new();

    public NodeQueueService()
    {
        _queue = Channel.CreateUnbounded<NextNodeData>();
    }

    public void Enqueue(NextNodeData nextNode)
    {
        _queue.Writer.TryWrite(nextNode);
    }

    public void Enqueue(ThreadContext threadContext, NodeEntity node)
    {
        Enqueue(new NextNodeData(threadContext, node));
    }

    public async ValueTask<NextNodeData> DequeueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var item = await _queue.Reader.ReadAsync(cancellationToken);
            if (!_blockedRuns.ContainsKey(item.ThreadContext.ProcessContext.Run.RunId))
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
