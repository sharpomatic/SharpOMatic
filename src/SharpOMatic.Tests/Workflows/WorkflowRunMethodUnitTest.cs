namespace SharpOMatic.Tests.Workflows;

public sealed class WorkflowRunMethodUnitTest
{
    [Fact]
    public async Task Workflow_can_start_and_wait()
    {
        var workflow = CreateWorkflow();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            ContextObject input = [];
            input.Set<string>("input.value", "wait");

            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, input);
            Assert.Equal(RunStatus.Success, run.RunStatus);

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("wait", output.Get<string>("output.value"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Workflow_can_start_and_notify()
    {
        var workflow = CreateWorkflow();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            ContextObject input = [];
            input.Set<string>("input.value", "notify");

            var runId = await engineService.StartWorkflowRunAndNotify(workflow.Id, input);
            var run = await WaitForRun(repositoryService, runId, RunStatus.Success);

            Assert.Equal(RunStatus.Success, run.RunStatus);
            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("notify", output.Get<string>("output.value"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Workflow_can_start_synchronously()
    {
        var workflow = CreateWorkflow();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            using var scope = provider.CreateScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            ContextObject input = [];
            input.Set<string>("input.value", "sync");

            var run = engineService.StartWorkflowRunSynchronously(workflow.Id, input);
            Assert.Equal(RunStatus.Success, run.RunStatus);

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("sync", output.Get<string>("output.value"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static WorkflowEntity CreateWorkflow()
    {
        return new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "Context.Set<string>(\"output.value\", Context.Get<string>(\"input.value\"));")
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();
    }

    private static async Task<Run> WaitForRun(IRepositoryService repositoryService, Guid runId, RunStatus expectedStatus)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var run = await repositoryService.GetRun(runId);
            if (run is not null && run.RunStatus == expectedStatus)
                return run;

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException($"Run '{runId}' did not reach run status '{expectedStatus}'.");
    }
}
