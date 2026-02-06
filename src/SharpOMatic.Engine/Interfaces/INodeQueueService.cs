namespace SharpOMatic.Engine.Interfaces;

public interface INodeQueueService
{
    void Enqueue(ThreadContext threadContext, NodeEntity node);
    ValueTask<(ThreadContext threadContext, NodeEntity node)> DequeueAsync(CancellationToken cancellationToken);
    void RemoveRunNodes(Guid runId);
}
