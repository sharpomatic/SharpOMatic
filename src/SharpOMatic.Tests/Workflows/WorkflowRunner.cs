
namespace SharpOMatic.Tests.Workflows;

public class WorkflowRunner
{
    public static async Task<Run> RunWorkflow(ContextObject ctx, params WorkflowEntity[] workflows)
    {
        using var provider = BuildProvider();

        // Add all provided workflows to the database
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        foreach (var workflow in workflows)
            await repositoryService.UpsertWorkflow(workflow);

        // Provide cancellation token so we can kill the node execution after the test
        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            // Run the first provided workflow
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engine.StartWorkflowRunAndWait(workflows[0].Id, ctx);
            await Task.Delay(100);
            return run;
        }
        finally
        {
            // Always kill the background node executor, wait for it to die before exiting
            cts.Cancel();
            await queueTask;
        }
    }

    public static async Task<(Run, ServiceProvider)> RunWorkflowDebug(ContextObject ctx, params WorkflowEntity[] workflows)
    {
        var provider = BuildProvider();

        // Add all provided workflows to the database
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        foreach (var workflow in workflows)
            await repositoryService.UpsertWorkflow(workflow);

        // Provide cancellation token so we can kill the node execution after the test
        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            // Run the first provided workflow
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engine.StartWorkflowRunAndWait(workflows[0].Id, ctx);
            await Task.Delay(100);
            return (run, provider);
        }
        finally
        {
            // Always kill the background node executor, wait for it to die before exiting
            cts.Cancel();
            await queueTask;
        }
    }

    public static ServiceProvider BuildProvider(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddSharpOMaticEngine().AddScriptOptions([typeof(WorkflowRunner).Assembly], ["SharpOMatic.Tests.Workflows"]).AddJsonConverters(typeof(ClassExampleConverter));

        // Override the repository with a simple in memory test version
        services.AddSingleton<IRepositoryService, TestRepositoryService>();

        var assetStoreMock = new Mock<IAssetStore>();
        var assetStorage = new ConcurrentDictionary<string, byte[]>();
        assetStoreMock
            .Setup(store => store.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns<string, Stream, CancellationToken>(async (storageKey, stream, _) =>
            {
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                assetStorage[storageKey] = memory.ToArray();
            });
        assetStoreMock
            .Setup(store => store.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((storageKey, _) =>
            {
                if (!assetStorage.TryGetValue(storageKey, out var data))
                    throw new FileNotFoundException($"Asset '{storageKey}' cannot be found.");

                return Task.FromResult<Stream>(new MemoryStream(data, writable: false));
            });
        assetStoreMock
            .Setup(store => store.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((storageKey, _) => Task.FromResult(assetStorage.ContainsKey(storageKey)));
        assetStoreMock
            .Setup(store => store.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((storageKey, cancellationToken) =>
            {
                assetStorage.TryRemove(storageKey, out var removedBytes);
                return Task.CompletedTask;
            });
        services.AddSingleton(assetStoreMock.Object);
        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    public static int Double(int value) => value * 2;
}
