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
                        if (threadContext.ProcessContext.Run.RunStatus != RunStatus.Failed)
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
        var processContext = threadContext.ProcessContext;

        try
        {
            threadContext.NodeId = node.Id;

            if (threadContext.CurrentContext is BatchContext batchContext &&
                batchContext.ContinueNodeId == node.Id)
            {
                if (batchContext.Parent is null)
                    throw new SharpOMaticException("Batch context is missing a parent execution context.");

                threadContext.CurrentContext = batchContext.Parent;
                processContext.UntrackContext(batchContext);
            }

            if (processContext.Run.RunStatus == RunStatus.Failed)
                return;

            if (!processContext.TryIncrementNodesRun(out _))
                throw new SharpOMaticException($"Hit run node limit of {processContext.RunNodeLimit}");

            var nextNodes = await RunNode(threadContext, node);
            if (processContext.Run.RunStatus == RunStatus.Failed)
                return;

            var continues = nextNodes.Any(nextNode => ReferenceEquals(nextNode.ThreadContext, threadContext));
            if (!continues)
                processContext.RemoveThread(threadContext);

            if (processContext.UpdateThreadCount(nextNodes.Count - 1) == 0)
            {
                if (processContext.Run.RunStatus == RunStatus.Failed)
                    return;

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
        var processContext = threadContext.ProcessContext;

        processContext.Run.RunStatus = runStatus;
        processContext.Run.Message = message;
        processContext.Run.Error = error;
        processContext.Run.Stopped = DateTime.Now;

        // If no EndNode was encountered then use the output of the last run node
        if (processContext.Run.OutputContext is null)
            processContext.Run.OutputContext = threadContext.NodeContext.Serialize(processContext.JsonConverters);

        await processContext.RunUpdated();

        processContext.CompletionSource?.TrySetResult(processContext.Run);

        await PruneRunHistory(processContext);

        var notifications = processContext.ServiceScope.ServiceProvider.GetServices<IEngineNotification>();
        foreach (var notification in notifications)
        {
            // Notify in separate task in case called instance perform a long running action
            _ = Task.Run(async () =>
            {
                await notification.RunCompleted(processContext.Run.RunId, processContext.Run.WorkflowId, processContext.Run.RunStatus,
                                                processContext.Run.OutputContext, processContext.Run.Error);

            });
        }

        processContext.ServiceScope.Dispose();
    }

    private Task<List<NextNodeData>> RunNode(ThreadContext threadContext, NodeEntity node)
    {
        var runner = runNodeFactory.Create(threadContext, node);
        return runner.Run();
    }

    private async Task PruneRunHistory(ProcessContext processContext)
    {
        try
        {
            var runHistoryLimitSetting = await processContext.RepositoryService.GetSetting("RunHistoryLimit");

            var runHistoryLimit = runHistoryLimitSetting?.ValueInteger ?? DEFAULT_RUN_HISTORY_LIMIT;
            if (runHistoryLimit < 0)
                runHistoryLimit = 0;

            await processContext.RepositoryService.PruneWorkflowRuns(processContext.Run.WorkflowId, runHistoryLimit);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to prune run history for workflow '{processContext.Run.WorkflowId}': {ex.Message}");
        }
    }
}
