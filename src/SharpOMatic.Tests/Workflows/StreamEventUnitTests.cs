namespace SharpOMatic.Tests.Workflows;

public sealed class StreamEventUnitTests
{
    [Fact]
    public async Task Code_node_stream_events_use_string_message_ids()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                await Events.AddTextStartAsync(StreamMessageRole.Assistant, "message-1", "start");
                await Events.AddTextContentAsync("message-1", "Hello");
                await Events.AddTextEndAsync("message-1", "end");
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
            Assert.Equal(3, streamEvents.Count);
            Assert.All(streamEvents, streamEvent =>
            {
                Assert.Equal("message-1", streamEvent.MessageId);
                Assert.Null(streamEvent.ConversationId);
            });
            Assert.Equal([1, 2, 3], streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal(StreamEventKind.TextStart, streamEvents[0].EventKind);
            Assert.Equal(StreamEventKind.TextContent, streamEvents[1].EventKind);
            Assert.Equal(StreamEventKind.TextEnd, streamEvents[2].EventKind);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_reasoning_events_persist_full_visible_reasoning_sequence()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddReasoningMessageAsync("reason-1", "Thinking about it");""")
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
            Assert.Equal(5, streamEvents.Count);
            Assert.Equal(
                [
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal(["reason-1", "reason-1", "reason-1", "reason-1", "reason-1"], streamEvents.Select(e => e.MessageId ?? string.Empty).ToArray());
            Assert.Equal([1, 2, 3, 4, 5], streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal(StreamMessageRole.Reasoning, streamEvents[1].MessageRole);
            Assert.Equal("Thinking about it", streamEvents[2].TextDelta);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_tool_call_events_persist_full_lifecycle_without_result()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddToolCallAsync("call-1", "lookup_weather", "{\"city\":\"Sydney\"}", "assistant-1");""")
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
            Assert.Equal(3, streamEvents.Count);
            Assert.Equal(
                [StreamEventKind.ToolCallStart, StreamEventKind.ToolCallArgs, StreamEventKind.ToolCallEnd],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal([1, 2, 3], streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal(["call-1", "call-1", "call-1"], streamEvents.Select(e => e.MessageId ?? string.Empty).ToArray());
            Assert.Equal(["call-1", "call-1", "call-1"], streamEvents.Select(e => e.ToolCallId ?? string.Empty).ToArray());
            Assert.Equal("lookup_weather", streamEvents[0].TextDelta);
            Assert.Equal("assistant-1", streamEvents[0].ParentMessageId);
            Assert.Equal("{\"city\":\"Sydney\"}", streamEvents[1].TextDelta);
            Assert.Null(streamEvents[1].ParentMessageId);
            Assert.Null(streamEvents[2].TextDelta);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Silent_tool_call_with_result_stream_events_apply_to_each_generated_live_progress_event()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """await Events.AddToolCallWithResultAsync("call-1", "lookup_weather", "{\"city\":\"Sydney\"}", "tool-result-1", "Sunny", "assistant-1", silent: true);"""
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services => services.AddSingleton<IProgressService>(progress));
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
            Assert.Equal(
                [StreamEventKind.ToolCallStart, StreamEventKind.ToolCallArgs, StreamEventKind.ToolCallEnd, StreamEventKind.ToolCallResult],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal(["call-1", "call-1", "call-1", "tool-result-1"], streamEvents.Select(e => e.MessageId ?? string.Empty).ToArray());
            Assert.Equal(["call-1", "call-1", "call-1", "call-1"], streamEvents.Select(e => e.ToolCallId ?? string.Empty).ToArray());
            Assert.Equal("Sunny", streamEvents[3].TextDelta);

            Assert.Equal(4, progress.StreamEventKinds.Count);
            Assert.All(progress.SilentFlags, Assert.True);
            Assert.Equal(
                [StreamEventKind.ToolCallStart, StreamEventKind.ToolCallArgs, StreamEventKind.ToolCallEnd, StreamEventKind.ToolCallResult],
                progress.StreamEventKinds.ToArray()
            );
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_low_level_tool_call_helpers_emit_expected_single_events()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                await Events.AddToolCallStartAsync("call-1", "lookup_weather", "assistant-1");
                await Events.AddToolCallArgsAsync("call-1", "{\"city\":\"Sydney\"}");
                await Events.AddToolCallEndAsync("call-1");
                await Events.AddToolCallResultAsync("tool-result-1", "call-1", "");
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
            Assert.Equal(4, streamEvents.Count);
            Assert.Equal(
                [StreamEventKind.ToolCallStart, StreamEventKind.ToolCallArgs, StreamEventKind.ToolCallEnd, StreamEventKind.ToolCallResult],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal("assistant-1", streamEvents[0].ParentMessageId);
            Assert.Equal(string.Empty, streamEvents[3].TextDelta);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Silent_text_stream_events_remain_persisted_but_are_marked_silent_in_live_progress()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddTextMessageAsync(StreamMessageRole.User, "message-1", "Hello", silent: true);""")
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services => services.AddSingleton<IProgressService>(progress));
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
            Assert.Equal(
                [StreamEventKind.TextStart, StreamEventKind.TextContent, StreamEventKind.TextEnd],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal([1, 2, 3], streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.All(streamEvents, streamEvent => Assert.Equal("message-1", streamEvent.MessageId));

            Assert.Equal(3, progress.StreamEventKinds.Count);
            Assert.All(progress.SilentFlags, Assert.True);
            Assert.Equal(
                [StreamEventKind.TextStart, StreamEventKind.TextContent, StreamEventKind.TextEnd],
                progress.StreamEventKinds.ToArray()
            );
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Silent_reasoning_stream_events_apply_to_each_generated_live_progress_event()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddReasoningMessageAsync("reason-1", "Thinking about it", silent: true);""")
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services => services.AddSingleton<IProgressService>(progress));
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
            Assert.Equal(5, streamEvents.Count);
            Assert.All(streamEvents, streamEvent => Assert.Equal("reason-1", streamEvent.MessageId));

            Assert.Equal(5, progress.StreamEventKinds.Count);
            Assert.All(progress.SilentFlags, Assert.True);
            Assert.Equal(
                [
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                ],
                progress.StreamEventKinds.ToArray()
            );
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_stream_events_require_non_empty_message_ids()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddTextStartAsync(StreamMessageRole.Assistant, "   ");""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("MessageId must be a non-empty string.", run.Error);
    }

    [Fact]
    public async Task Code_node_reasoning_events_require_non_empty_content()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddReasoningMessageContentAsync("reason-1", "   ");""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Reasoning message delta cannot be empty or whitespace.", run.Error);
    }

    [Fact]
    public async Task Code_node_tool_call_events_require_non_empty_tool_call_id()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddToolCallStartAsync("   ", "lookup_weather");""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("ToolCallId must be a non-empty string.", run.Error);
    }

    [Fact]
    public async Task Code_node_tool_call_events_require_non_empty_tool_name()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddToolCallStartAsync("call-1", "   ");""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Tool call name cannot be empty or whitespace.", run.Error);
    }

    [Fact]
    public async Task Code_node_tool_call_events_require_non_empty_args()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddToolCallArgsAsync("call-1", "   ");""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Tool call args cannot be empty or whitespace.", run.Error);
    }

    [Fact]
    public async Task Code_node_tool_call_result_events_require_non_empty_result_message_id()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddToolCallResultAsync("   ", "call-1", "Sunny");""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Tool call result message id must be a non-empty string.", run.Error);
    }

    [Fact]
    public async Task Code_node_tool_call_result_events_reject_null_content()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddToolCallResultAsync("tool-result-1", "call-1", null!);""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Tool call result cannot be null.", run.Error);
    }

    [Fact]
    public async Task Code_node_step_start_events_persist_step_name_in_text_delta()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddStepStartAsync("Search");""")
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
            var stepEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.StepStart, stepEvent.EventKind);
            Assert.Equal("Search", stepEvent.TextDelta);
            Assert.Null(stepEvent.MessageId);
            Assert.Null(stepEvent.ActivityType);
            Assert.Null(stepEvent.ToolCallId);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_step_end_events_persist_step_name_in_text_delta()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddStepEndAsync("Search");""")
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
            var stepEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.StepEnd, stepEvent.EventKind);
            Assert.Equal("Search", stepEvent.TextDelta);
            Assert.Null(stepEvent.MessageId);
            Assert.Null(stepEvent.ActivityType);
            Assert.Null(stepEvent.ToolCallId);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Silent_step_stream_events_apply_to_each_generated_live_progress_event()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                await Events.AddStepStartAsync("Search", silent: true);
                await Events.AddStepEndAsync("Search", silent: true);
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services => services.AddSingleton<IProgressService>(progress));
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
            Assert.Equal([StreamEventKind.StepStart, StreamEventKind.StepEnd], streamEvents.Select(e => e.EventKind).ToArray());
            Assert.Equal(["Search", "Search"], streamEvents.Select(e => e.TextDelta ?? string.Empty).ToArray());

            Assert.Equal(2, progress.StreamEventKinds.Count);
            Assert.All(progress.SilentFlags, Assert.True);
            Assert.Equal([StreamEventKind.StepStart, StreamEventKind.StepEnd], progress.StreamEventKinds.ToArray());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_step_events_require_non_empty_step_name()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddStepStartAsync("   ");""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Step name must be a non-empty string.", run.Error);
    }

    [Fact]
    public async Task Code_node_activity_snapshot_events_persist_snapshot_details()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """await Events.AddActivitySnapshotAsync("activity-1", "PLAN", new { steps = new[] { new { title = "Search", status = "in_progress" } } }, replace: false);"""
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
            var activityEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.ActivitySnapshot, activityEvent.EventKind);
            Assert.Equal("activity-1", activityEvent.MessageId);
            Assert.Equal("PLAN", activityEvent.ActivityType);
            Assert.False(activityEvent.Replace);

            using var payload = JsonDocument.Parse(activityEvent.TextDelta!);
            Assert.Equal(JsonValueKind.Object, payload.RootElement.ValueKind);
            Assert.Equal("Search", payload.RootElement.GetProperty("steps")[0].GetProperty("title").GetString());
            Assert.Equal("in_progress", payload.RootElement.GetProperty("steps")[0].GetProperty("status").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_activity_delta_events_persist_patch_details()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """await Events.AddActivityDeltaAsync("activity-1", "PLAN", new object[] { new { op = "replace", path = "/steps/0/status", value = "done" } });"""
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
            var activityEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.ActivityDelta, activityEvent.EventKind);
            Assert.Equal("activity-1", activityEvent.MessageId);
            Assert.Equal("PLAN", activityEvent.ActivityType);
            Assert.Null(activityEvent.Replace);

            using var payload = JsonDocument.Parse(activityEvent.TextDelta!);
            Assert.Equal(JsonValueKind.Array, payload.RootElement.ValueKind);
            Assert.Equal("replace", payload.RootElement[0].GetProperty("op").GetString());
            Assert.Equal("/steps/0/status", payload.RootElement[0].GetProperty("path").GetString());
            Assert.Equal("done", payload.RootElement[0].GetProperty("value").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_activity_sync_from_context_emits_snapshot_on_first_call_and_persists_hidden_state()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var step = new ContextObject();
                step.Add("title", "Search");
                step.Add("status", "in_progress");
                var steps = new ContextList();
                steps.Add(step);
                var activity = new ContextObject();
                activity.Add("steps", steps);
                Context.Set("activity", activity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity", replace: false);
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
            var jsonConverters = scope.ServiceProvider.GetRequiredService<IJsonConverterService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            var activityEvent = Assert.Single(streamEvents);
            Assert.Equal(StreamEventKind.ActivitySnapshot, activityEvent.EventKind);
            Assert.Equal("activity-1", activityEvent.MessageId);
            Assert.Equal("PLAN", activityEvent.ActivityType);

            var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("activity-1", output.Get<string>("_hidden.activity[0].instanceName"));
            Assert.Equal("PLAN", output.Get<string>("_hidden.activity[0].activityType"));
            Assert.Equal("Search", output.Get<string>("_hidden.activity[0].content.steps[0].title"));
            Assert.Equal("in_progress", output.Get<string>("_hidden.activity[0].content.steps[0].status"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_activity_sync_from_context_stores_detached_copy()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var step = new ContextObject();
                step.Add("title", "Search");
                step.Add("status", "in_progress");
                var steps = new ContextList();
                steps.Add(step);
                var activity = new ContextObject();
                activity.Add("steps", steps);
                Context.Set("activity", activity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");

                Context.Set("activity.steps[0].status", "done");
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var (run, provider) = await WorkflowRunner.RunWorkflowDebug([], workflow);
        Assert.Equal(RunStatus.Success, run.RunStatus);

        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
        Assert.Equal("done", output.Get<string>("activity.steps[0].status"));
        Assert.Equal("in_progress", output.Get<string>("_hidden.activity[0].content.steps[0].status"));
    }

    [Fact]
    public async Task Code_node_activity_sync_from_context_emits_patch_when_smaller_and_activity_type_matches()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var activity = new ContextObject();
                activity.Add("title", new string('x', 200));
                activity.Add("status", "in_progress");
                Context.Set("activity", activity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
                Context.Set("activity.status", "done");
                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
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
            Assert.Equal([StreamEventKind.ActivitySnapshot, StreamEventKind.ActivityDelta], streamEvents.Select(e => e.EventKind).ToArray());
            Assert.Equal(["PLAN", "PLAN"], streamEvents.Select(e => e.ActivityType ?? string.Empty).ToArray());

            using var patch = JsonDocument.Parse(streamEvents[1].TextDelta!);
            Assert.Equal("replace", patch.RootElement[0].GetProperty("op").GetString());
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
    public async Task Code_node_activity_sync_from_context_recurses_nested_objects()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var details = new ContextObject();
                details.Add("status", "in_progress");
                var activity = new ContextObject();
                activity.Add("title", new string('x', 200));
                activity.Add("details", details);
                Context.Set("activity", activity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
                Context.Set("activity.details.status", "done");
                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
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
            using var patch = JsonDocument.Parse(streamEvents[1].TextDelta!);
            Assert.Equal("/details/status", patch.RootElement[0].GetProperty("path").GetString());
            Assert.Equal("done", patch.RootElement[0].GetProperty("value").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_activity_sync_from_context_falls_back_to_snapshot_when_patch_would_be_larger()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var items = new ContextList();
                items.Add("one");
                items.Add("two");
                var activity = new ContextObject();
                activity.Add("items", items);
                Context.Set("activity", activity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
                var updatedItems = new ContextList();
                updatedItems.Add("one");
                updatedItems.Add("two");
                updatedItems.Add("three");
                Context.Set("activity.items", updatedItems);
                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
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
            Assert.Equal([StreamEventKind.ActivitySnapshot, StreamEventKind.ActivitySnapshot], streamEvents.Select(e => e.EventKind).ToArray());
            Assert.True(streamEvents[1].Replace);
            using var snapshot = JsonDocument.Parse(streamEvents[1].TextDelta!);
            Assert.Equal(JsonValueKind.Array, snapshot.RootElement.GetProperty("items").ValueKind);
            Assert.Equal(3, snapshot.RootElement.GetProperty("items").GetArrayLength());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_activity_sync_from_context_no_op_emits_no_event_and_refreshes_stored_copy()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var firstStep = new ContextObject();
                firstStep.Add("title", "Search");
                firstStep.Add("status", "in_progress");
                var firstSteps = new ContextList();
                firstSteps.Add(firstStep);
                var firstActivity = new ContextObject();
                firstActivity.Add("steps", firstSteps);
                Context.Set("activity", firstActivity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");

                var previousStored = Context.Get<ContextObject>("_hidden.activity[0].content");

                var secondStep = new ContextObject();
                secondStep.Add("title", "Search");
                secondStep.Add("status", "in_progress");
                var secondSteps = new ContextList();
                secondSteps.Add(secondStep);
                var secondActivity = new ContextObject();
                secondActivity.Add("steps", secondSteps);
                Context.Set("activity", secondActivity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");

                var currentStored = Context.Get<ContextObject>("_hidden.activity[0].content");
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
        var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
        Assert.Single(streamEvents);
        Assert.Equal(StreamEventKind.ActivitySnapshot, streamEvents[0].EventKind);

        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
        Assert.False(output.Get<bool>("output.sameStoredReference"));
    }

    [Fact]
    public async Task Code_node_activity_sync_from_context_requires_matching_activity_type_for_existing_instance()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var activity = new ContextObject();
                activity.Add("status", "in_progress");
                Context.Set("activity", activity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
                await Events.AddActivitySyncFromContextAsync("activity-1", "STATUS", "activity");
                """
            )
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Activity instance 'activity-1' is already associated with activity type 'PLAN', not 'STATUS'.", run.Error);
    }

    [Fact]
    public async Task Code_node_activity_sync_from_context_requires_json_object_root()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var activity = new ContextList();
                activity.Add("bad");
                Context.Set("activity", activity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
                """
            )
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Activity sync requires context path 'activity' to resolve to a JSON object.", run.Error);
    }

    [Fact]
    public async Task Code_node_activity_sync_from_context_requires_json_object_root_on_later_calls()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var activity = new ContextObject();
                activity.Add("status", "in_progress");
                Context.Set("activity", activity);
                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");

                var invalid = new ContextList();
                invalid.Add("bad");
                Context.Set("activity", invalid);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
                """
            )
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Activity sync requires context path 'activity' to resolve to a JSON object.", run.Error);
    }

    [Fact]
    public async Task Silent_activity_stream_events_apply_to_each_generated_live_progress_event()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                await Events.AddActivitySnapshotAsync("activity-1", "PLAN", new { steps = new[] { new { title = "Search", status = "in_progress" } } }, replace: false, silent: true);
                await Events.AddActivityDeltaAsync("activity-1", "PLAN", new object[] { new { op = "replace", path = "/steps/0/status", value = "done" } }, silent: true);
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services => services.AddSingleton<IProgressService>(progress));
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

            Assert.Equal(2, progress.StreamEventKinds.Count);
            Assert.All(progress.SilentFlags, Assert.True);
            Assert.Equal([StreamEventKind.ActivitySnapshot, StreamEventKind.ActivityDelta], progress.StreamEventKinds.ToArray());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_activity_snapshot_events_require_non_empty_activity_type()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddActivitySnapshotAsync("activity-1", "   ", new { steps = new[] { new { title = "Search" } } });""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("ActivityType must be a non-empty string.", run.Error);
    }

    [Fact]
    public async Task Code_node_activity_snapshot_events_require_json_object_content()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddActivitySnapshotAsync("activity-1", "PLAN", new[] { "bad" });""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("ActivitySnapshot stream events require TextDelta to contain a JSON object.", run.Error);
    }

    [Fact]
    public async Task Code_node_activity_delta_events_require_valid_json_patch_shape()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddActivityDeltaAsync("activity-1", "PLAN", new object[] { new { op = "replace", value = "done" } });""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("must include a non-empty 'path' property", run.Error);
    }

    [Fact]
    public async Task Code_node_custom_event_persists_name_and_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddCustomEventAsync("weather_progress", "{\"stage\":\"fetch\"}");""")
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

            var customEvent = Assert.Single(await repositoryService.GetRunStreamEvents(run.RunId));
            Assert.Equal(StreamEventKind.Custom, customEvent.EventKind);
            Assert.Equal("weather_progress", customEvent.TextDelta);
            Assert.Equal("{\"stage\":\"fetch\"}", customEvent.Metadata);
            Assert.Null(customEvent.MessageId);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Code_node_custom_event_requires_non_empty_name()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", """await Events.AddCustomEventAsync("   ", "value");""")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Custom event name must be a non-empty string.", run.Error);
    }

    [Fact]
    public async Task Conversation_stream_events_use_request_stream_conversation_id_and_continue_sequence()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("first", """await Events.AddTextMessageAsync(StreamMessageRole.Assistant, "message-1", "First");""")
            .AddSuspend("ask")
            .AddCode("second", """await Events.AddTextMessageAsync(StreamMessageRole.Assistant, "message-2", "Second");""")
            .AddEnd()
            .Connect("start", "first")
            .Connect("first", "ask")
            .Connect("ask", "second")
            .Connect("second", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();
        const string streamConversationId = "ag-ui-conversation-1";

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);
            await WaitForConversationStatusAsync(repositoryService, conversationId, ConversationStatus.Suspended);
            await Task.Delay(100);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var streamEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            Assert.Equal(6, streamEvents.Count);
            Assert.All(streamEvents, streamEvent => Assert.Equal(streamConversationId, streamEvent.ConversationId));
            Assert.Equal([1, 2, 3, 4, 5, 6], streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal(new string?[] { "message-1", "message-1", "message-1", "message-2", "message-2", "message-2" }, streamEvents.Select(e => e.MessageId).ToArray());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_activity_stream_events_use_request_stream_conversation_id_and_continue_sequence()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode(
                "code",
                """
                var turn = Context.TryGet<int>("state.turn", out var currentTurn) ? currentTurn + 1 : 1;
                Context.Set<int>("state.turn", turn);

                if (turn == 1)
                {
                    var step = new ContextObject();
                    step.Add("title", "Search");
                    step.Add("status", "in_progress");
                    var steps = new ContextList();
                    steps.Add(step);
                    var activity = new ContextObject();
                    activity.Add("title", new string('x', 200));
                    activity.Add("steps", steps);
                    Context.Set("activity", activity);

                    await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity", replace: false);
                }
                else
                {
                    Context.Set("activity.steps[0].status", "done");
                    await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
                }
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
        var conversationId = NewConversationId();
        const string streamConversationId = "activity-conversation-1";

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.Equal(RunStatus.Success, firstTurn.RunStatus);
            await WaitForConversationStatusAsync(repositoryService, conversationId, ConversationStatus.Completed);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var streamEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            Assert.Equal(2, streamEvents.Count);
            Assert.All(streamEvents, streamEvent => Assert.Equal(streamConversationId, streamEvent.ConversationId));
            Assert.Equal([1, 2], streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal([StreamEventKind.ActivitySnapshot, StreamEventKind.ActivityDelta], streamEvents.Select(e => e.EventKind).ToArray());
            Assert.Equal(["activity-1", "activity-1"], streamEvents.Select(e => e.MessageId ?? string.Empty).ToArray());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_step_stream_events_use_request_stream_conversation_id_and_continue_sequence()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode(
                "code",
                """
                var turn = Context.TryGet<int>("state.turn", out var currentTurn) ? currentTurn + 1 : 1;
                Context.Set<int>("state.turn", turn);

                if (turn == 1)
                    await Events.AddStepStartAsync("Search");
                else
                    await Events.AddStepEndAsync("Search");
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
        var conversationId = NewConversationId();
        const string streamConversationId = "step-conversation-1";

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.Equal(RunStatus.Success, firstTurn.RunStatus);
            await WaitForConversationStatusAsync(repositoryService, conversationId, ConversationStatus.Completed);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var streamEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            Assert.Equal(2, streamEvents.Count);
            Assert.All(streamEvents, streamEvent => Assert.Equal(streamConversationId, streamEvent.ConversationId));
            Assert.Equal([1, 2], streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal([StreamEventKind.StepStart, StreamEventKind.StepEnd], streamEvents.Select(e => e.EventKind).ToArray());
            Assert.Equal(["Search", "Search"], streamEvents.Select(e => e.TextDelta ?? string.Empty).ToArray());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_tool_call_stream_events_use_request_stream_conversation_id_and_continue_sequence()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("first", """await Events.AddToolCallAsync("call-1", "lookup_weather", "{\"city\":\"Sydney\"}", "assistant-1");""")
            .AddSuspend("ask")
            .AddCode("second", """await Events.AddToolCallWithResultAsync("call-2", "lookup_time", "{\"timezone\":\"Australia/Sydney\"}", "tool-result-2", "10:00", "assistant-2");""")
            .AddEnd()
            .Connect("start", "first")
            .Connect("first", "ask")
            .Connect("ask", "second")
            .Connect("second", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();
        const string streamConversationId = "tool-call-conversation-1";

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var streamEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            Assert.Equal(7, streamEvents.Count);
            Assert.All(streamEvents, streamEvent => Assert.Equal(streamConversationId, streamEvent.ConversationId));
            Assert.Equal([1, 2, 3, 4, 5, 6, 7], streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal(
                [
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                    StreamEventKind.ToolCallResult,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal(
                new string?[] { "call-1", "call-1", "call-1", "call-2", "call-2", "call-2", "tool-result-2" },
                streamEvents.Select(e => e.MessageId).ToArray()
            );
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_stream_events_allow_null_stream_conversation_id()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("code", """await Events.AddTextMessageAsync(StreamMessageRole.Assistant, "message-1", "Hello");""")
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
            var run = await engineService.StartOrResumeConversationAndWait(workflow.Id, NewConversationId());

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.NotEmpty(streamEvents);
            Assert.All(streamEvents, streamEvent => Assert.Null(streamEvent.ConversationId));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_stream_events_reject_ids_longer_than_256_characters()
    {
        var workflow = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();

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
            var streamConversationId = new string('x', 257);

            var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
                engineService.StartOrResumeConversationAndWait(workflow.Id, NewConversationId(), streamConversationId: streamConversationId)
            );

            Assert.Equal("Stream conversation id cannot be longer than 256 characters.", exception.Message);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static string NewConversationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static async Task WaitForConversationStatusAsync(IRepositoryService repositoryService, string conversationId, ConversationStatus expectedStatus)
    {
        for (var attempt = 0; attempt < 20; attempt += 1)
        {
            var conversation = await repositoryService.GetConversation(conversationId);
            if (conversation?.Status == expectedStatus && string.IsNullOrWhiteSpace(conversation.LeaseOwner))
                return;

            await Task.Delay(25);
        }

        var currentConversation = await repositoryService.GetConversation(conversationId);
        Assert.Equal(expectedStatus, currentConversation?.Status);
        Assert.True(string.IsNullOrWhiteSpace(currentConversation?.LeaseOwner));
    }

    private sealed class CapturingProgressService : IProgressService
    {
        public List<StreamEventKind> StreamEventKinds { get; } = [];
        public List<bool> SilentFlags { get; } = [];

        public Task RunProgress(Run run)
        {
            return Task.CompletedTask;
        }

        public Task TraceProgress(Run run, Trace trace)
        {
            return Task.CompletedTask;
        }

        public Task InformationsProgress(Run run, List<Information> informations)
        {
            return Task.CompletedTask;
        }

        public Task StreamEventProgress(Run run, List<StreamEventProgressItem> events)
        {
            StreamEventKinds.AddRange(events.Select(e => e.Event.EventKind));
            SilentFlags.AddRange(events.Select(e => e.Silent));
            return Task.CompletedTask;
        }

        public Task EvalRunProgress(EvalRun evalRun)
        {
            return Task.CompletedTask;
        }
    }
}
