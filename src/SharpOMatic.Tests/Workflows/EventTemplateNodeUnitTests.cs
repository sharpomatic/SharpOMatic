namespace SharpOMatic.Tests.Workflows;

public sealed class EventTemplateNodeUnitTests
{
    [Fact]
    public async Task Text_mode_emits_text_message_with_expanded_template_and_selected_role()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEventTemplate("event", "Hello {{$input.name}}", textRole: StreamMessageRole.User)
            .AddEnd()
            .Connect("start", "event")
            .Connect("event", "end")
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
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, new ContextObject { ["input"] = new ContextObject { ["name"] = "Ada" } });

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal([StreamEventKind.TextStart, StreamEventKind.TextContent, StreamEventKind.TextEnd], streamEvents.Select(e => e.EventKind).ToArray());
            Assert.Equal(StreamMessageRole.User, streamEvents[0].MessageRole);
            Assert.Equal("Hello Ada", streamEvents[1].TextDelta);
            Assert.All(streamEvents, streamEvent => Assert.StartsWith("event-template-", streamEvent.MessageId));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Reasoning_mode_emits_reasoning_lifecycle()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEventTemplate("event", "Thinking about {{$input.topic}}", EventTemplateOutputMode.Reasoning)
            .AddEnd()
            .Connect("start", "event")
            .Connect("event", "end")
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
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, new ContextObject { ["input"] = new ContextObject { ["topic"] = "weather" } });

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
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
            Assert.Equal(StreamMessageRole.Reasoning, streamEvents[1].MessageRole);
            Assert.Equal("Thinking about weather", streamEvents[2].TextDelta);
            Assert.All(streamEvents, streamEvent => Assert.StartsWith("event-template-", streamEvent.MessageId));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Blank_expanded_output_emits_no_stream_events_and_continues()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEventTemplate("event", "  {{$missing.path}}  ")
            .AddCode("after", """Context.Set("output.route", "continued");""")
            .AddEnd()
            .Connect("start", "event")
            .Connect("event", "after")
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
            Assert.Empty(await repositoryService.GetRunStreamEvents(run.RunId));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Silent_mode_persists_events_and_marks_live_progress_silent()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEventTemplate("event", "Hello", silent: true)
            .AddEnd()
            .Connect("start", "event")
            .Connect("event", "end")
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
            Assert.Equal([StreamEventKind.TextStart, StreamEventKind.TextContent, StreamEventKind.TextEnd], (await repositoryService.GetRunStreamEvents(run.RunId)).Select(e => e.EventKind).ToArray());
            Assert.Equal([StreamEventKind.TextStart, StreamEventKind.TextContent, StreamEventKind.TextEnd], progress.StreamEventKinds.ToArray());
            Assert.All(progress.SilentFlags, Assert.True);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Template_expansion_uses_context_and_text_assets()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "asset",
                """
                var bytes = System.Text.Encoding.UTF8.GetBytes("Asset says {{$input.topic}}");
                await Assets.AddAssetFromBytesAsync(bytes, "prompt.txt", "text/plain");
                """
            )
            .AddEventTemplate("event", "<<prompt.txt>>")
            .AddEnd()
            .Connect("start", "asset")
            .Connect("asset", "event")
            .Connect("event", "end")
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
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, new ContextObject { ["input"] = new ContextObject { ["topic"] = "templates" } });

            Assert.Equal(RunStatus.Success, run.RunStatus);
            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal("Asset says templates", streamEvents.Single(e => e.EventKind == StreamEventKind.TextContent).TextDelta);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Text_mode_rejects_reasoning_role()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEventTemplate("event", "Hello", textRole: StreamMessageRole.Reasoning)
            .Connect("start", "event")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Event Template text output cannot use the Reasoning role.", run.Error);
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
