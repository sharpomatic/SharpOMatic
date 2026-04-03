namespace SharpOMatic.Engine.Interfaces;

public interface INodeQueueService
{
    void Enqueue(NextNodeData nextNode);
    void Enqueue(ThreadContext threadContext, NodeEntity node);
    ValueTask<NextNodeData> DequeueAsync(CancellationToken cancellationToken);
    void RemoveRunNodes(Guid runId);
}
