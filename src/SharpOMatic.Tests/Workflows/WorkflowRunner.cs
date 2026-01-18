using SharpOMatic.Engine.Contexts;

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
            var runId = await engine.CreateWorkflowRun(workflows[0].Id);
            var run = await engine.StartWorkflowRunAndWait(runId, ctx);
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
            var runId = await engine.CreateWorkflowRun(workflows[0].Id);
            var run = await engine.StartWorkflowRunAndWait(runId, ctx);
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

    public static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSharpOMaticEngine()
                .AddScriptOptions([typeof(WorkflowRunner).Assembly], ["SharpOMatic.Tests.Workflows"])
                .AddJsonConverters(typeof(ClassExampleConverter));

        // Override the repository with a simple in memory test version
        services.AddSingleton<IRepositoryService, TestRepositoryService>();

        return services.BuildServiceProvider();
    }

    public static int Double(int value) => value * 2;
}
