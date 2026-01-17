namespace SharpOMatic.Engine.Interfaces;

public interface INodeExecutionService
{
    Task RunQueueAsync(CancellationToken cancellationToken = default);
}
