namespace SharpOMatic.Engine.Services;

public class NodeExecutionService(INodeQueueService queue, IRunNodeFactory runNodeFactory) : INodeExecutionService
{
    public const int DEFAULT_RUN_HISTORY_LIMIT = 50;
    public const int DEFAULT_NODE_RUN_LIMIT = 500;
    private readonly SemaphoreSlim _semaphore = new(5);

    public async Task RunQueueAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                var (threadContext, node) = await queue.DequeueAsync(cancellationToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // If the workflow has already been failed, then ignore the node execution
                        if (threadContext.RunContext.Run.RunStatus != RunStatus.Failed)
                            await ProcessNode(threadContext, node);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _semaphore.Release();

                // Graceful shutdown
                break;
            }
            catch
            {
                _semaphore.Release();
                throw;
            }
        }
    }

    private async Task ProcessNode(ThreadContext threadContext, NodeEntity node)
    {
        var runContext = threadContext.RunContext;

        try
        {
            if (runContext.Run.RunStatus == RunStatus.Failed)
                return;

            if (!runContext.TryIncrementNodesRun(out _))
                throw new SharpOMaticException($"Hit run node limit of {runContext.RunNodeLimit}");

            var nextNodes = await RunNode(threadContext, node);
            if (runContext.Run.RunStatus == RunStatus.Failed)
                return;

            if (runContext.UpdateThreadCount(nextNodes.Count - 1) == 0)
            {
                if (runContext.Run.RunStatus == RunStatus.Failed)
                    return;

                // When a FanIn completes and there is no next thread, continue with parent and not the last child to arrive
                if (node is FanInNodeEntity)
                    threadContext = threadContext.Parent ?? threadContext;

                await RunCompleted(threadContext, RunStatus.Success, "Success");
            }
            else
            {
                foreach (var nextNode in nextNodes)
                    queue.Enqueue(nextNode.ThreadContext, nextNode.Node);
            }
        }
        catch (Exception ex)
        {
            await RunCompleted(threadContext, RunStatus.Failed, "Failed", ex.Message);
        }
    }

    private async Task RunCompleted(ThreadContext threadContext, RunStatus runStatus, string message, string? error = "")
    {
        var runContext = threadContext.RunContext;

        runContext.Run.RunStatus = runStatus;
        runContext.Run.Message = message;
        runContext.Run.Error = error;
        runContext.Run.Stopped = DateTime.Now;

        // If no EndNode was encountered then use the output of the last run node
        if (threadContext.RunContext.Run.OutputContext is null)
            runContext.Run.OutputContext = threadContext.NodeContext.Serialize(threadContext.RunContext.JsonConverters);

        await runContext.RunUpdated();

        if (runContext.CompletionSource is not null)
            runContext.CompletionSource.SetResult(runContext.Run);

        await PruneRunHistory(runContext);

        var notifications = runContext.ServiceScope.ServiceProvider.GetServices<IEngineNotification>();
        foreach (var notification in notifications)
        {
            // Notify in separate task in case called instance perform a long running action
            _ = Task.Run(async () =>
            {
                await notification.RunCompleted(runContext.Run.RunId, runContext.Run.WorkflowId, runContext.Run.RunStatus,
                                                runContext.Run.OutputContext, runContext.Run.Error);

            });
        }

        runContext.ServiceScope.Dispose();
    }

    private Task<List<NextNodeData>> RunNode(ThreadContext threadContext, NodeEntity node)
    {
        var runner = runNodeFactory.Create(threadContext, node);
        return runner.Run();
    }

    private async Task PruneRunHistory(RunContext runContext)
    {
        try
        {
            var runHistoryLimitSetting = await runContext.RepositoryService.GetSetting("RunHistoryLimit");

            var runHistoryLimit = runHistoryLimitSetting?.ValueInteger ?? DEFAULT_RUN_HISTORY_LIMIT;
            if (runHistoryLimit < 0)
                runHistoryLimit = 0;

            await runContext.RepositoryService.PruneWorkflowRuns(runContext.Run.WorkflowId, runHistoryLimit);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to prune run history for workflow '{runContext.Run.WorkflowId}': {ex.Message}");
        }
    }
}
