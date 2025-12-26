namespace SharpOMatic.Engine.Services;

public class NodeExecutionService(INodeQueue queue, IServiceScopeFactory scopeFactory) : BackgroundService
{
    public const int DEFAULT_RUN_HISTORY_LIMIT = 50;
    public const int DEFAULT_NODE_RUN_LIMIT = 500;

    private static readonly Dictionary<NodeType, Type> _nodeRunners;
    private readonly SemaphoreSlim _semaphore = new(5);

    static NodeExecutionService()
    {
        _nodeRunners = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<RunNodeAttribute>() != null)
            .ToDictionary(t => t.GetCustomAttribute<RunNodeAttribute>()!.NodeType, t => t);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CheckSettings();
        await LoadMetadata();

        while (!stoppingToken.IsCancellationRequested)
        {
            await _semaphore.WaitAsync(stoppingToken);

            try
            {
                var (threadContext, node) = await queue.DequeueAsync(stoppingToken);

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
                }, stoppingToken);
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

                runContext.Run.RunStatus = RunStatus.Success;
                runContext.Run.Message = "Success";
                runContext.Run.Stopped = DateTime.Now;

                // If no EndNode was encountered then use the output of the last run node
                if (runContext.Run.OutputContext is null)
                    runContext.Run.OutputContext = runContext.TypedSerialization(threadContext.NodeContext);

                await runContext.RunUpdated();
                runContext.ServiceScope.Dispose();
            }
            else
            {
                foreach (var nextNode in nextNodes)
                    queue.Enqueue(nextNode.ThreadContext, nextNode.Node);
            }
        }
        catch (Exception ex)
        {
            runContext.Run.RunStatus = RunStatus.Failed;
            runContext.Run.Message = "Failed";
            runContext.Run.Error = ex.Message;
            runContext.Run.Stopped = DateTime.Now;

            // If no EndNode was encountered then use the output of the last run node
            if (runContext.Run.OutputContext is null)
                runContext.Run.OutputContext = runContext.TypedSerialization(threadContext.NodeContext);

            await runContext.RunUpdated();
            runContext.ServiceScope.Dispose();
        }
    }

    private static Task<List<NextNodeData>> RunNode(ThreadContext threadContext, NodeEntity node)
    {
        if (_nodeRunners.TryGetValue(node.NodeType, out var runnerType))
        {
            var runner = (IRunNode)Activator.CreateInstance(runnerType, threadContext, node)!;
            return runner.Run();
        }

        throw new SharpOMaticException($"Unrecognized node type '{node.NodeType}'");
    }

    private async Task LoadMetadata()
    {
        await LoadMetadata<ConnectorConfig>("Metadata.Resources.ConnectorConfig", (repo, config) => repo.UpsertConnectorConfig(config));
        await LoadMetadata<ModelConfig>("Metadata.Resources.ModelConfig", (repo, config) => repo.UpsertModelConfig(config));
    }

    private async Task LoadMetadata<T>(string resourceFilter, Func<IRepository, T, Task> upsertAction)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames().Where(name => name.Contains(resourceFilter) && name.EndsWith(".json"));

        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var config = await JsonSerializer.DeserializeAsync<T>(stream);
                    if (config != null)
                        await upsertAction(repository, config);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load metadata from {resourceName}: {ex.Message}");
            }
        }
    }

    private async Task CheckSettings()
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();
        var versionSetting = await repository.GetSettings().FirstOrDefaultAsync(s => s.Name == "Version");

        if (versionSetting is null)
        {
            await repository.UpsertSetting(new Setting()
            {
                SettingId = Guid.NewGuid(),
                Name = "Version",
                DisplayName = "Version",
                UserEditable = false,
                SettingType = SettingType.Integer,
                ValueInteger = 1
            });

            await repository.UpsertSetting(new Setting()
            {
                SettingId = Guid.NewGuid(),
                Name = "RunHistoryLimit",
                DisplayName = "Run History Limit",
                UserEditable = true,
                SettingType = SettingType.Integer,
                ValueInteger = DEFAULT_RUN_HISTORY_LIMIT
            });

            await repository.UpsertSetting(new Setting()
            {
                SettingId = Guid.NewGuid(),
                Name = "RunNodeLimit",
                UserEditable = true,
                DisplayName = "Run Node Limit",
                SettingType = SettingType.Integer,
                ValueInteger = DEFAULT_NODE_RUN_LIMIT
            });
        }
    }

}
