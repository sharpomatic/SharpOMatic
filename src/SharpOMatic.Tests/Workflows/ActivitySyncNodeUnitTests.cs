namespace SharpOMatic.Tests.Workflows;

public sealed class ActivitySyncNodeUnitTests
{
    [Fact]
    public async Task Node_sync_emits_initial_snapshot_uses_configured_replace_and_continues()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "seed",
                """
                var activity = new ContextObject();
                activity.Add("status", "running");
                Context.Set("activity", activity);
                """
            )
            .AddActivitySync(title: "sync", instanceName: "activity-1", activityType: "PLAN", contextPath: "activity", initialReplace: true)
            .AddCode("after", """Context.Set("output.route", "continued");""")
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "sync")
            .Connect("sync", "after")
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

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            var activityEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.ActivitySnapshot, activityEvent.EventKind);
            Assert.True(activityEvent.Replace);
            Assert.Equal("activity-1", activityEvent.MessageId);
            Assert.Equal("PLAN", activityEvent.ActivityType);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Node_sync_emits_delta_when_patch_is_smaller()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "seed",
                """
                var activity = new ContextObject();
                activity.Add("title", new string('x', 200));
                activity.Add("status", "running");
                Context.Set("activity", activity);
                """
            )
            .AddActivitySync(title: "sync-1", instanceName: "activity-1", activityType: "PLAN", contextPath: "activity")
            .AddCode("update", """Context.Set("activity.status", "completed");""")
            .AddActivitySync(title: "sync-2", instanceName: "activity-1", activityType: "PLAN", contextPath: "activity")
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
            Assert.Equal([StreamEventKind.ActivitySnapshot, StreamEventKind.ActivityDelta], streamEvents.Select(e => e.EventKind).ToArray());

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
    public async Task Node_sync_falls_back_to_replacement_snapshot_when_patch_is_larger()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "seed",
                """
                var items = new ContextList();
                items.Add("one");
                items.Add("two");
                var activity = new ContextObject();
                activity.Add("items", items);
                Context.Set("activity", activity);
                """
            )
            .AddActivitySync(title: "sync-1", instanceName: "activity-1", activityType: "PLAN", contextPath: "activity")
            .AddCode(
                "update",
                """
                var updatedItems = new ContextList();
                updatedItems.Add("one");
                updatedItems.Add("two");
                updatedItems.Add("three");
                Context.Set("activity.items", updatedItems);
                """
            )
            .AddActivitySync(title: "sync-2", instanceName: "activity-1", activityType: "PLAN", contextPath: "activity")
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
            Assert.Equal([StreamEventKind.ActivitySnapshot, StreamEventKind.ActivitySnapshot], streamEvents.Select(e => e.EventKind).ToArray());
            Assert.True(streamEvents[1].Replace);

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
    public async Task Node_sync_rejects_activity_type_mismatch_for_existing_instance()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "seed",
                """
                var activity = new ContextObject();
                activity.Add("status", "running");
                Context.Set("activity", activity);
                """
            )
            .AddActivitySync(title: "sync-1", instanceName: "activity-1", activityType: "PLAN", contextPath: "activity")
            .AddActivitySync(title: "sync-2", instanceName: "activity-1", activityType: "STATUS", contextPath: "activity")
            .Connect("start", "seed")
            .Connect("seed", "sync-1")
            .Connect("sync-1", "sync-2")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Activity instance 'activity-1' is already associated with activity type 'PLAN', not 'STATUS'.", run.Error);
    }
}
