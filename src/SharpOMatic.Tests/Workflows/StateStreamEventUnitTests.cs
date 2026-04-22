namespace SharpOMatic.Tests.Workflows;

public sealed class StateStreamEventUnitTests
{
    [Theory]
    [InlineData("""{"mode":"assistant"}""")]
    [InlineData("""["a","b"]""")]
    [InlineData("123")]
    [InlineData("null")]
    public async Task Code_node_state_snapshot_events_accept_any_json_root(string snapshotJson)
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                $$"""
                await Events.AddStateSnapshotAsync(ContextHelpers.FastDeserializeString({{JsonSerializer.Serialize(snapshotJson)}}));
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
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

            var stateEvent = Assert.Single(await repositoryService.GetRunStreamEvents(run.RunId));
            Assert.Equal(StreamEventKind.StateSnapshot, stateEvent.EventKind);
            Assert.Equal(snapshotJson, stateEvent.TextDelta);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_state_delta_events_require_valid_json_patch_shape()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddStateDeltaAsync(new object[] { new { op = "replace", value = "done" } });""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("StateDelta JSON Patch operation at index 0 must include a non-empty 'path' property.", run.Error);
    }

    [Fact]
    public async Task Code_node_state_sync_no_ops_when_agent_state_is_missing()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddStateSyncAsync();""")
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var (run, provider) = await WorkflowRunner.RunWorkflowDebug([], workflow);
        Assert.Equal(RunStatus.Success, run.RunStatus);

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        Assert.Empty(await repositoryService.GetRunStreamEvents(run.RunId));
    }

    [Fact]
    public async Task Code_node_state_sync_emits_snapshot_on_first_call_and_persists_hidden_state()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                Context.Set("agent.state", new ContextObject()
                {
                    ["mode"] = "assistant",
                });

                await Events.AddStateSyncAsync();
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var (run, provider) = await WorkflowRunner.RunWorkflowDebug([], workflow);
        Assert.Equal(RunStatus.Success, run.RunStatus);

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        var stateEvent = Assert.Single(await repositoryService.GetRunStreamEvents(run.RunId));
        Assert.Equal(StreamEventKind.StateSnapshot, stateEvent.EventKind);

        var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
        Assert.Equal("assistant", output.Get<string>("agent._hidden.state.mode"));
    }

    [Fact]
    public async Task Code_node_state_sync_emits_delta_when_patch_is_smaller()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                Context.Set("agent.state", new ContextObject()
                {
                    ["title"] = new string('x', 200),
                    ["status"] = "running",
                });

                await Events.AddStateSyncAsync();
                Context.Set("agent.state.status", "done");
                await Events.AddStateSyncAsync();
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
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
            Assert.Equal("/status", patch.RootElement[0].GetProperty("path").GetString());
            Assert.Equal("done", patch.RootElement[0].GetProperty("value").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_state_sync_falls_back_to_snapshot_when_patch_is_larger_or_root_replaced()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                Context.Set("agent.state", new ContextObject() { ["mode"] = "assistant" });
                await Events.AddStateSyncAsync();
                Context.Set("agent.state", new ContextList() { "a", "b", "c" });
                await Events.AddStateSyncAsync();
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
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
            Assert.Equal("""["a","b","c"]""", streamEvents[1].TextDelta);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_state_sync_no_op_refreshes_stored_copy()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                Context.Set("agent.state", new ContextObject() { ["mode"] = "assistant" });
                await Events.AddStateSyncAsync();

                var previousStored = Context.Get<ContextObject>("agent._hidden.state");
                Context.Set("agent.state", new ContextObject() { ["mode"] = "assistant" });
                await Events.AddStateSyncAsync();

                var currentStored = Context.Get<ContextObject>("agent._hidden.state");
                Context.Set("output.sameStoredReference", object.ReferenceEquals(previousStored, currentStored));
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var (run, provider) = await WorkflowRunner.RunWorkflowDebug([], workflow);
        Assert.Equal(RunStatus.Success, run.RunStatus);

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
        Assert.Single(streamEvents);

        var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
        Assert.False(output.Get<bool>("output.sameStoredReference"));
    }

    [Fact]
    public async Task State_stream_events_are_serialized_without_context_type_metadata()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var details = new ContextObject();
                details.Add("title", new string('x', 200));
                details.Add("status", "running");
                Context.Set("agent.state", details);
                await Events.AddStateSyncAsync();
                Context.Set("agent.state.status", "done");
                await Events.AddStateSyncAsync();
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
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

            var deltaEvent = Assert.Single(await repositoryService.GetRunStreamEvents(run.RunId), e => e.EventKind == StreamEventKind.StateDelta);
            Assert.DoesNotContain("\"$type\"", deltaEvent.TextDelta, StringComparison.Ordinal);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }
}
