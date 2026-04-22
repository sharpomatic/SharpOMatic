namespace SharpOMatic.Tests.Workflows;

public sealed class StateSyncNodeUnitTests
{
    [Fact]
    public async Task State_sync_emits_initial_snapshot_and_continues()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """Context.Set("agent.state", new ContextObject() { ["status"] = "running" });""")
            .AddStateSync()
            .AddCode("after", """Context.Set("output.route", "continued");""")
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "State Sync")
            .Connect("State Sync", "after")
            .Connect("after", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("continued", output.Get<string>("output.route"));
            Assert.Equal("running", output.Get<string>("agent._hidden.state.status"));

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            var stateEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.StateSnapshot, stateEvent.EventKind);

            using var snapshot = JsonDocument.Parse(stateEvent.TextDelta!);
            Assert.Equal("running", snapshot.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task State_sync_emits_delta_when_patch_is_smaller()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "seed",
                """
                Context.Set("agent.state", new ContextObject()
                {
                    ["title"] = new string('x', 200),
                    ["status"] = "running",
                });
                """
            )
            .AddStateSync(title: "sync-1")
            .AddCode("update", """Context.Set("agent.state.status", "completed");""")
            .AddStateSync(title: "sync-2")
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "sync-1")
            .Connect("sync-1", "update")
            .Connect("update", "sync-2")
            .Connect("sync-2", "end")
            .Build();

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
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal([StreamEventKind.StateSnapshot, StreamEventKind.StateDelta], streamEvents.Select(e => e.EventKind).ToArray());

            using var patch = JsonDocument.Parse(streamEvents[1].TextDelta!);
            Assert.Equal("replace", patch.RootElement[0].GetProperty("op").GetString());
            Assert.Equal("/status", patch.RootElement[0].GetProperty("path").GetString());
            Assert.Equal("completed", patch.RootElement[0].GetProperty("value").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task State_sync_falls_back_to_snapshot_when_patch_is_larger()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "seed",
                """
                var items = new ContextList() { "one", "two" };
                Context.Set("agent.state", new ContextObject() { ["items"] = items });
                """
            )
            .AddStateSync(title: "sync-1")
            .AddCode(
                "update",
                """
                var updatedItems = new ContextList() { "one", "two", "three" };
                Context.Set("agent.state.items", updatedItems);
                """
            )
            .AddStateSync(title: "sync-2")
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "sync-1")
            .Connect("sync-1", "update")
            .Connect("update", "sync-2")
            .Connect("sync-2", "end")
            .Build();

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
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal([StreamEventKind.StateSnapshot, StreamEventKind.StateSnapshot], streamEvents.Select(e => e.EventKind).ToArray());

            using var snapshot = JsonDocument.Parse(streamEvents[1].TextDelta!);
            Assert.Equal(3, snapshot.RootElement.GetProperty("items").GetArrayLength());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task State_sync_no_ops_when_agent_state_is_missing()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddStateSync()
            .AddCode("after", """Context.Set("output.route", "continued");""")
            .AddEnd()
            .Connect("start", "State Sync")
            .Connect("State Sync", "after")
            .Connect("after", "end")
            .Build();

        var (run, provider) = await WorkflowRunner.RunWorkflowDebug([], workflow);
        Assert.Equal(RunStatus.Success, run.RunStatus);

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
        Assert.Empty(streamEvents);

        var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
        Assert.Equal("continued", output.Get<string>("output.route"));
        Assert.False(output.TryGet<object?>("agent._hidden.state", out _));
    }

    [Fact]
    public async Task State_sync_snapshots_only_emits_snapshots_even_when_delta_is_smaller()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "seed",
                """
                Context.Set("agent.state", new ContextObject()
                {
                    ["title"] = new string('x', 200),
                    ["status"] = "running",
                });
                """
            )
            .AddStateSync(title: "sync-1", snapshotsOnly: true)
            .AddCode("update", """Context.Set("agent.state.status", "completed");""")
            .AddStateSync(title: "sync-2", snapshotsOnly: true)
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "sync-1")
            .Connect("sync-1", "update")
            .Connect("update", "sync-2")
            .Connect("sync-2", "end")
            .Build();

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
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal([StreamEventKind.StateSnapshot, StreamEventKind.StateSnapshot], streamEvents.Select(e => e.EventKind).ToArray());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }
}
