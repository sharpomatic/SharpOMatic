
namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallStreamingUnitTests
{
    [Fact]
    public async Task Model_call_emits_resolved_prompt_stream_events_before_model_output()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);
        var modelNode = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(n => n.Title == "model"));
        modelNode.Prompt = "Say {{$input.topic}}";
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, PromptEventTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        var context = new ContextObject();
        context.Set("input.topic", "hello");

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, context);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal(
                [
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );

            var userMessageId = streamEvents[0].MessageId ?? string.Empty;
            Assert.False(string.IsNullOrWhiteSpace(userMessageId));
            Assert.Equal(StreamMessageRole.User, streamEvents[0].MessageRole);
            Assert.Equal("Say hello", streamEvents[1].TextDelta);
            Assert.Equal([userMessageId, userMessageId, userMessageId], streamEvents.Take(3).Select(e => e.MessageId ?? string.Empty).ToArray());

            Assert.Equal(StreamMessageRole.Assistant, streamEvents[3].MessageRole);
            Assert.Equal("Assistant reply", streamEvents[4].TextDelta);
            Assert.Equal(Enumerable.Range(1, 6).ToArray(), streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal([true, true, true, false, false, false], progress.StreamEventSilentFlags.ToArray());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_resolves_recursive_context_and_asset_prompt_templates()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);
        var modelNode = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(n => n.Title == "model"));
        modelNode.Prompt = "Prompt {{$input.template}}";

        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, PromptEventTestModelCaller>("openai"));

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        var assetService = new AssetService(repositoryService, provider.GetRequiredService<IAssetStore>());
        await assetService.CreateFromBytesAsync(Encoding.UTF8.GetBytes("for {{$input.topic}}"), "prompt-fragment.txt", "text/plain", AssetScope.Library);

        var context = new ContextObject();
        context.Set("input.template", "using <<prompt-fragment.txt>>");
        context.Set("input.topic", "hello");

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, context);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal("Prompt using for hello", streamEvents.Where(e => e.EventKind == StreamEventKind.TextContent).First().TextDelta);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_can_disable_user_stream_events_without_affecting_model_prompt_or_output()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId, disableStreamUser: true);
        var modelNode = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(n => n.Title == "model"));
        modelNode.Prompt = "Say {{$input.topic}}";
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, PromptEventTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        var context = new ContextObject();
        context.Set("input.topic", "hello");

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, context);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal(
                [
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );

            Assert.Equal(StreamMessageRole.Assistant, streamEvents[0].MessageRole);
            Assert.Equal("Assistant reply", streamEvents[1].TextDelta);
            Assert.Equal([false, false, false], progress.StreamEventSilentFlags.ToArray());

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("Assistant reply", output.Get<string>("output.text"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_does_not_write_text_output_when_text_output_path_is_blank()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);
        var modelNode = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(n => n.Title == "model"));
        modelNode.TextOutputPath = string.Empty;

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddKeyedScoped<IModelCaller, PromptEventTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.False(output.TryGet<string>("output.text", out _));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_streams_reasoning_events_and_informations_before_completion()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId, chatInputPath: "history", chatOutputPath: "output.chat");
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, StreamingTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        var context = new ContextObject();
        context.Set(
            "history",
            new ContextList()
            {
                new ChatMessage(ChatRole.User, "Earlier question"),
            }
        );

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, context);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal(14, streamEvents.Count);
            Assert.Equal(
                [
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal(Enumerable.Range(1, 14).ToArray(), streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal(["Hello", " world"], streamEvents.Where(e => e.EventKind == StreamEventKind.TextContent).Select(e => e.TextDelta ?? string.Empty).ToArray());
            Assert.Equal(["Thinking"], streamEvents.Where(e => e.EventKind == StreamEventKind.ReasoningMessageContent).Select(e => e.TextDelta ?? string.Empty).ToArray());
            Assert.Equal(StreamMessageRole.Reasoning, streamEvents.Single(e => e.EventKind == StreamEventKind.ReasoningMessageStart).MessageRole);
            Assert.Equal(
                [
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "call-1",
                    "call-1",
                    "call-1",
                ],
                streamEvents.Select(e => e.MessageId ?? string.Empty).ToArray()
            );

            var toolCallStart = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallStart);
            Assert.Equal("call-1", toolCallStart.MessageId);
            Assert.Equal("call-1", toolCallStart.ToolCallId);
            Assert.Equal("lookup_weather", toolCallStart.TextDelta);
            Assert.Equal("assistant-1", toolCallStart.ParentMessageId);
            var toolCallArgs = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallArgs);
            Assert.Equal("call-1", toolCallArgs.ToolCallId);
            Assert.Equal("{\"city\":\"Sydney\"}", toolCallArgs.TextDelta);
            var toolCallEnd = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallEnd);
            Assert.Equal("call-1", toolCallEnd.MessageId);
            Assert.Equal("call-1", toolCallEnd.ToolCallId);

            var informations = await repositoryService.GetRunInformations(run.RunId);
            Assert.Equal(4, informations.Count);
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == "Hello"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == " world"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Reasoning) && (i.Text == "Thinking"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.ToolCall) && (i.Text == "lookup_weather"));

            Assert.NotEmpty(progress.StreamEventStatuses);
            Assert.NotEmpty(progress.InformationStatuses);
            Assert.All(progress.StreamEventStatuses, status => Assert.Equal(RunStatus.Running, status));
            Assert.All(progress.InformationStatuses, status => Assert.Equal(RunStatus.Running, status));
            Assert.Contains(StreamEventKind.ReasoningStart, progress.StreamEventKinds);
            Assert.Contains(StreamEventKind.ReasoningMessageContent, progress.StreamEventKinds);
            Assert.Contains(StreamEventKind.ReasoningEnd, progress.StreamEventKinds);
            Assert.Contains(StreamEventKind.ToolCallStart, progress.StreamEventKinds);
            Assert.Contains(StreamEventKind.ToolCallArgs, progress.StreamEventKinds);
            Assert.Contains(StreamEventKind.ToolCallEnd, progress.StreamEventKinds);
            Assert.All(progress.StreamEventSilentFlags, Assert.False);
            Assert.Equal(
                ["Assistant:Hello", "Reasoning:Thinking", "Assistant: world", "ToolCall:lookup_weather"],
                progress.InformationLog.Take(4)
            );

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("Hello world", output.Get<string>("output.text"));

            var chatOutput = output.Get<ContextList>("output.chat");
            Assert.Equal(2, chatOutput.Count);
            Assert.Equal(ChatRole.User, ((ChatMessage)chatOutput[0]!).Role);
            Assert.Equal(ChatRole.Assistant, ((ChatMessage)chatOutput[1]!).Role);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_can_disable_assistant_text_stream_events_without_affecting_outputs_or_informations()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId, disableStreamAssistantText: true);
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, StreamingTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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
                [
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.DoesNotContain(streamEvents, e => e.EventKind is StreamEventKind.TextStart or StreamEventKind.TextContent or StreamEventKind.TextEnd);

            var informations = await repositoryService.GetRunInformations(run.RunId);
            Assert.Equal(4, informations.Count);
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == "Hello"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == " world"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Reasoning) && (i.Text == "Thinking"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.ToolCall) && (i.Text == "lookup_weather"));

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("Hello world", output.Get<string>("output.text"));
            Assert.DoesNotContain(progress.StreamEventKinds, kind => kind is StreamEventKind.TextStart or StreamEventKind.TextContent or StreamEventKind.TextEnd);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_can_disable_reasoning_stream_events_while_keeping_reasoning_information()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId, disableStreamReasoning: true);
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, StreamingTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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
                [
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.DoesNotContain(streamEvents, e => e.EventKind is StreamEventKind.ReasoningStart or StreamEventKind.ReasoningMessageStart or StreamEventKind.ReasoningMessageContent or StreamEventKind.ReasoningMessageEnd or StreamEventKind.ReasoningEnd);

            var informations = await repositoryService.GetRunInformations(run.RunId);
            Assert.Equal(4, informations.Count);
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == "Hello"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == " world"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Reasoning) && (i.Text == "Thinking"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.ToolCall) && (i.Text == "lookup_weather"));

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("Hello world", output.Get<string>("output.text"));
            Assert.DoesNotContain(progress.StreamEventKinds, kind => kind is StreamEventKind.ReasoningStart or StreamEventKind.ReasoningMessageStart or StreamEventKind.ReasoningMessageContent or StreamEventKind.ReasoningMessageEnd or StreamEventKind.ReasoningEnd);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_can_disable_tool_stream_events_while_keeping_tool_information()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId, disableStreamTool: true);
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, StreamingTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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
                [
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.DoesNotContain(streamEvents, e => e.EventKind is StreamEventKind.ToolCallStart or StreamEventKind.ToolCallArgs or StreamEventKind.ToolCallEnd or StreamEventKind.ToolCallResult);

            var informations = await repositoryService.GetRunInformations(run.RunId);
            Assert.Equal(4, informations.Count);
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == "Hello"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == " world"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Reasoning) && (i.Text == "Thinking"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.ToolCall) && (i.Text == "lookup_weather"));

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("Hello world", output.Get<string>("output.text"));
            Assert.DoesNotContain(progress.StreamEventKinds, kind => kind is StreamEventKind.ToolCallStart or StreamEventKind.ToolCallArgs or StreamEventKind.ToolCallEnd or StreamEventKind.ToolCallResult);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_model_call_reasoning_events_continue_sequence_across_turns()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddModelCall("first")
            .AddSuspend("pause")
            .AddModelCall("second")
            .AddEnd()
            .Connect("start", "first")
            .Connect("first", "pause")
            .Connect("pause", "second")
            .Connect("second", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "first", model.ModelId);
        ConfigureModelNode(workflow, "second", model.ModelId);

        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, StreamingConversationReasoningTestModelCaller>("openai"));
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = Guid.NewGuid().ToString("N");
        const string streamConversationId = "stream-conversation-model-call";

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, streamConversationId: streamConversationId);
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);
            await Task.Delay(100);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, streamConversationId: streamConversationId);
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var streamEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            Assert.Equal(22, streamEvents.Count);
            Assert.Equal(Enumerable.Range(1, 22).ToArray(), streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal(
                [
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-1",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                    "assistant-2",
                ],
                streamEvents.Select(e => e.MessageId ?? string.Empty).ToArray()
            );

            var output = ContextObject.Deserialize(secondTurn.OutputContext);
            Assert.True(output.TryGetObject("output", out var outputObject));
            Assert.NotNull(outputObject);
            Assert.Equal("Turn 2 reply", output.Get<string>("output.text"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_batch_model_call_synthesizes_unique_text_message_ids_across_turns()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddModelCall("first")
            .AddSuspend("pause")
            .AddModelCall("second")
            .AddEnd()
            .Connect("start", "first")
            .Connect("first", "pause")
            .Connect("pause", "second")
            .Connect("second", "end")
            .Build();

        var model = CreateModel("batch");
        ConfigureModelNode(workflow, "first", model.ModelId, batchOutput: true);
        ConfigureModelNode(workflow, "second", model.ModelId, batchOutput: true);

        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, BatchFallbackTestModelCaller>("batch"));
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "batch", model);
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = Guid.NewGuid().ToString("N");
        const string streamConversationId = "stream-conversation-model-call-batch";

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, streamConversationId: streamConversationId);
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);
            await Task.Delay(100);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, streamConversationId: streamConversationId);
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var streamEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            Assert.Equal(30, streamEvents.Count);
            Assert.Equal(Enumerable.Range(1, 30).ToArray(), streamEvents.Select(e => e.SequenceNumber).ToArray());

            var textStartIds = streamEvents
                .Where(e => e.EventKind == StreamEventKind.TextStart)
                .Select(e => e.MessageId ?? string.Empty)
                .ToArray();

            Assert.Equal(
                [
                    "assistant:batch:1:0",
                    "assistant:batch:1:0:text:1",
                    "assistant:batch:16:0",
                    "assistant:batch:16:0:text:1",
                ],
                textStartIds
            );
            Assert.Equal(textStartIds.Length, textStartIds.Distinct(StringComparer.Ordinal).Count());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Google_model_call_streams_reasoning_events_and_informations()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("google");
        ConfigureModelNode(workflow, "model", model.ModelId);
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, GoogleStreamingTestModelCaller>("google");
        });
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "google", model);
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
            Assert.Equal(20, streamEvents.Count);
            Assert.Equal(
                [
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                    StreamEventKind.ToolCallResult,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal(Enumerable.Range(1, 20).ToArray(), streamEvents.Select(e => e.SequenceNumber).ToArray());
            Assert.Equal(["Google", " reply"], streamEvents.Where(e => e.EventKind == StreamEventKind.TextContent).Select(e => e.TextDelta ?? string.Empty).ToArray());
            Assert.Equal(["Google reasoning", " final"], streamEvents.Where(e => e.EventKind == StreamEventKind.ReasoningMessageContent).Select(e => e.TextDelta ?? string.Empty).ToArray());
            var googleToolCallStart = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallStart);
            Assert.Equal("google-call-1", googleToolCallStart.MessageId);
            Assert.Equal("google-call-1", googleToolCallStart.ToolCallId);
            Assert.Equal("google-assistant-1", googleToolCallStart.ParentMessageId);
            var googleToolCallArgs = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallArgs);
            Assert.Equal("google-call-1", googleToolCallArgs.ToolCallId);
            Assert.Equal("{\"query\":\"weather\"}", googleToolCallArgs.TextDelta);
            var googleToolCallResult = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallResult);
            Assert.Equal("google-lookup-result-1", googleToolCallResult.MessageId);
            Assert.Equal("google-call-1", googleToolCallResult.ToolCallId);
            Assert.Equal("Sunny", googleToolCallResult.TextDelta);

            var informations = await repositoryService.GetRunInformations(run.RunId);
            Assert.Equal(4, informations.Count);
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == "Google"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == " reply"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Reasoning) && (i.Text == "Google reasoning final"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.ToolCall) && (i.Text == "google_lookup"));

            Assert.NotEmpty(progress.StreamEventStatuses);
            Assert.NotEmpty(progress.InformationStatuses);
            Assert.All(progress.StreamEventStatuses, status => Assert.Equal(RunStatus.Running, status));
            Assert.All(progress.InformationStatuses, status => Assert.Equal(RunStatus.Running, status));
            Assert.Contains(StreamEventKind.ToolCallResult, progress.StreamEventKinds);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Batch_model_call_creates_reasoning_stream_events_from_final_response()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("batch");
        ConfigureModelNode(workflow, "model", model.ModelId, batchOutput: true);
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, BatchFallbackTestModelCaller>("batch");
        });
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "batch", model);
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
            Assert.Equal(15, streamEvents.Count);
            Assert.Equal(
                [
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                    StreamEventKind.TextStart,
                    StreamEventKind.TextContent,
                    StreamEventKind.TextEnd,
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                    StreamEventKind.ToolCallResult,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
            Assert.Equal(
                [
                    "assistant:batch:1:0",
                    "assistant:batch:1:0",
                    "assistant:batch:1:0",
                    "assistant:batch:1:0",
                    "assistant:batch:1:0",
                    "assistant:batch:1:0",
                    "assistant:batch:1:0",
                    "assistant:batch:1:0",
                    "assistant:batch:1:0:text:1",
                    "assistant:batch:1:0:text:1",
                    "assistant:batch:1:0:text:1",
                    "batch-call-1",
                    "batch-call-1",
                    "batch-call-1",
                    "tool-result:batch:1:0:0",
                ],
                streamEvents.Select(e => e.MessageId ?? string.Empty).ToArray()
            );
            Assert.Equal(["Batch", " reply"], streamEvents.Where(e => e.EventKind == StreamEventKind.TextContent).Select(e => e.TextDelta ?? string.Empty).ToArray());
            Assert.Equal(["Batch reasoning"], streamEvents.Where(e => e.EventKind == StreamEventKind.ReasoningMessageContent).Select(e => e.TextDelta ?? string.Empty).ToArray());
            var batchToolCallStart = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallStart);
            Assert.Equal("batch-call-1", batchToolCallStart.MessageId);
            Assert.Equal("batch-call-1", batchToolCallStart.ToolCallId);
            Assert.Equal("assistant:batch:1:0:text:1", batchToolCallStart.ParentMessageId);
            var batchToolCallArgs = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallArgs);
            Assert.Equal("batch-call-1", batchToolCallArgs.ToolCallId);
            Assert.Equal("{\"city\":\"Melbourne\"}", batchToolCallArgs.TextDelta);
            var batchToolCallResult = streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallResult);
            Assert.Equal("tool-result:batch:1:0:0", batchToolCallResult.MessageId);
            Assert.Equal("batch-call-1", batchToolCallResult.ToolCallId);
            Assert.Equal("Batch tool result", batchToolCallResult.TextDelta);

            var informations = await repositoryService.GetRunInformations(run.RunId);
            Assert.Equal(4, informations.Count);
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == "Batch"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == " reply"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Reasoning) && (i.Text == "Batch reasoning"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.ToolCall) && (i.Text == "batch_lookup"));

            Assert.NotEmpty(progress.StreamEventStatuses);
            Assert.NotEmpty(progress.InformationStatuses);
            Assert.All(progress.StreamEventStatuses, status => Assert.Equal(RunStatus.Running, status));
            Assert.All(progress.InformationStatuses, status => Assert.Equal(RunStatus.Running, status));
            Assert.Contains(StreamEventKind.ReasoningMessageContent, progress.StreamEventKinds);
            Assert.Contains(StreamEventKind.ToolCallResult, progress.StreamEventKinds);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Batch_model_call_can_disable_all_stream_events_while_keeping_outputs_and_informations()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("batch");
        ConfigureModelNode(
            workflow,
            "model",
            model.ModelId,
            batchOutput: true,
            disableStreamTool: true,
            disableStreamReasoning: true,
            disableStreamAssistantText: true
        );
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, BatchFallbackTestModelCaller>("batch");
        });
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "batch", model);
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
            Assert.Empty(streamEvents);
            Assert.Empty(progress.StreamEventKinds);

            var informations = await repositoryService.GetRunInformations(run.RunId);
            Assert.Equal(4, informations.Count);
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == "Batch"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Assistant) && (i.Text == " reply"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.Reasoning) && (i.Text == "Batch reasoning"));
            Assert.Contains(informations, i => (i.InformationType == InformationType.ToolCall) && (i.Text == "batch_lookup"));

            var output = ContextObject.Deserialize(run.OutputContext);
            Assert.Equal("Batch reply", output.Get<string>("output.text"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_closes_reasoning_before_tool_call_information_progress()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, ReasoningThenToolCallTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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
            var infoIndex = progress.ProgressLog.IndexOf("info:ToolCall");
            Assert.True(infoIndex > 0);
            Assert.Contains("stream:ReasoningMessageEnd", progress.ProgressLog.Take(infoIndex));
            Assert.Contains("stream:ReasoningEnd", progress.ProgressLog.Take(infoIndex));
            Assert.Equal(["Reasoning:Thinking", "ToolCall:lookup_weather"], progress.InformationLog.Take(2));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Empty_reasoning_update_does_not_blank_existing_reasoning_information()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);

        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, EmptyReasoningAfterToolCallTestModelCaller>("openai"));
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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

            var informations = await repositoryService.GetRunInformations(run.RunId);
            var reasoning = Assert.Single(informations, i => i.InformationType == InformationType.Reasoning);
            Assert.Equal("Thinking", reasoning.Text);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal(
                [
                    StreamEventKind.ReasoningStart,
                    StreamEventKind.ReasoningMessageStart,
                    StreamEventKind.ReasoningMessageContent,
                    StreamEventKind.ReasoningMessageEnd,
                    StreamEventKind.ReasoningEnd,
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Empty_reasoning_without_text_creates_information_but_no_stream_events()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);

        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, EmptyReasoningOnlyTestModelCaller>("openai"));
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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

            var informations = await repositoryService.GetRunInformations(run.RunId);
            var reasoning = Assert.Single(informations, i => i.InformationType == InformationType.Reasoning);
            Assert.Equal(string.Empty, reasoning.Text);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.DoesNotContain(
                streamEvents,
                e => e.EventKind is StreamEventKind.ReasoningStart
                    or StreamEventKind.ReasoningMessageStart
                    or StreamEventKind.ReasoningMessageContent
                    or StreamEventKind.ReasoningMessageEnd
                    or StreamEventKind.ReasoningEnd
            );
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Assistant_text_without_reasoning_or_tool_call_creates_single_information_record_at_completion()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);

        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, AssistantTextOnlyTestModelCaller>("openai"));
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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

            var informations = await repositoryService.GetRunInformations(run.RunId);
            var assistant = Assert.Single(informations, i => i.InformationType == InformationType.Assistant);
            Assert.Equal("Hello world", assistant.Text);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Whitespace_assistant_text_does_not_create_information_record()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);

        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, WhitespaceAssistantTextTestModelCaller>("openai"));
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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

            var informations = await repositoryService.GetRunInformations(run.RunId);
            Assert.DoesNotContain(informations, i => i.InformationType == InformationType.Assistant);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Model_call_closes_text_before_tool_call_information_progress()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var model = CreateModel("openai");
        ConfigureModelNode(workflow, "model", model.ModelId);
        var progress = new CapturingProgressService();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IProgressService>(progress);
            services.AddKeyedScoped<IModelCaller, TextThenToolCallTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
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
            var infoIndex = progress.ProgressLog.IndexOf("info:ToolCall");
            Assert.True(infoIndex > 0);
            Assert.Contains("stream:TextEnd", progress.ProgressLog.Take(infoIndex));
            Assert.Contains("info:Assistant", progress.ProgressLog.Take(infoIndex));
            Assert.Equal(["Assistant:Hello", "ToolCall:lookup_weather"], progress.InformationLog.Take(2));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static void ConfigureModelNode(
        WorkflowEntity workflow,
        string title,
        Guid modelId,
        string chatInputPath = "",
        string chatOutputPath = "",
        bool batchOutput = false,
        bool disableStreamUser = false,
        bool disableStreamTool = false,
        bool disableStreamReasoning = false,
        bool disableStreamAssistantText = false
    )
    {
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(n => n.Title == title));
        node.ModelId = modelId;
        node.BatchOutput = batchOutput;
        node.DisableStreamUser = disableStreamUser;
        node.DisableStreamTool = disableStreamTool;
        node.DisableStreamReasoning = disableStreamReasoning;
        node.DisableStreamAssistantText = disableStreamAssistantText;
        node.TextOutputPath = "output.text";
        node.ChatInputPath = chatInputPath;
        node.ChatOutputPath = chatOutputPath;
        node.Prompt = "Say hello";
    }

    private static async Task SeedModelCallMetadata(IRepositoryService repositoryService, string configId, Model model)
    {
        await repositoryService.UpsertConnectorConfig(
            new ConnectorConfig()
            {
                Version = 1,
                ConfigId = configId,
                DisplayName = configId,
                Description = configId,
                AuthModes = [],
            }
        );

        await repositoryService.UpsertConnector(
            new Connector()
            {
                Version = 1,
                ConnectorId = model.ConnectorId!.Value,
                Name = $"{configId}-connector",
                Description = $"{configId}-connector",
                ConfigId = configId,
                AuthenticationModeId = string.Empty,
                FieldValues = [],
            }
        );

        await repositoryService.UpsertModelConfig(
            new ModelConfig()
            {
                Version = 1,
                ConfigId = $"{configId}-model-config",
                DisplayName = $"{configId}-model",
                Description = $"{configId}-model",
                ConnectorConfigId = configId,
                IsCustom = false,
                Information = null,
                Capabilities = [],
                ParameterFields = [],
            }
        );

        await repositoryService.UpsertModel(model);
    }

    private static Model CreateModel(string connectorConfigId)
    {
        return new Model()
        {
            Version = 1,
            ModelId = Guid.NewGuid(),
            Name = $"{connectorConfigId}-model",
            Description = $"{connectorConfigId}-model",
            ConfigId = $"{connectorConfigId}-model-config",
            ConnectorId = Guid.NewGuid(),
            CustomCapabilities = [],
            ParameterValues = [],
        };
    }

    private sealed class StreamingTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            List<ChatMessage> chat = [];
            if (!string.IsNullOrWhiteSpace(node.ChatInputPath) && threadContext.NodeContext.TryGet<ContextList>(node.ChatInputPath, out var chatList) && (chatList is not null))
            {
                foreach (var entry in chatList)
                    if (entry is ChatMessage message)
                        chat.Add(message);
            }

            await progressSink.OnTextStartAsync("assistant-1");
            await progressSink.OnTextDeltaAsync("assistant-1", "Hello");
            await progressSink.OnReasoningAsync("assistant-1", "Thinking");
            await progressSink.OnTextDeltaAsync("assistant-1", " world");
            await progressSink.OnToolCallAsync("call-1", "lookup_weather", "{\"city\":\"Sydney\"}", "assistant-1");
            await progressSink.CompleteAsync();

            IList<ChatMessage> responses =
            [
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new TextContent("Hello world"),
                        new TextReasoningContent("Thinking"),
                        new FunctionCallContent("call-1", "lookup_weather", new Dictionary<string, object?>() { ["city"] = "Sydney" }),
                    ]
                ),
            ];

            return (chat, responses, "Hello world");
        }
    }

    private sealed class PromptEventTestModelCaller : BaseModelCaller
    {
        public override async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            List<ChatMessage> chat = [];
            (var _, var prompt) = await ResolveInstructionsAndPrompt(chat, processContext, threadContext, node);
            await EmitPromptStreamEvents(processContext, prompt, node.DisableStreamUser);

            await progressSink.OnTextStartAsync("assistant-1");
            await progressSink.OnTextDeltaAsync("assistant-1", "Assistant reply");
            await progressSink.OnTextEndAsync("assistant-1");

            return (chat, [new ChatMessage(ChatRole.Assistant, [new TextContent("Assistant reply")])], "Assistant reply");
        }
    }

    private sealed class StreamingConversationReasoningTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            var turnNumber = processContext.Run.TurnNumber ?? 1;
            var messageId = $"assistant-{turnNumber}";
            var reasoningId = messageId;

            await progressSink.OnTextStartAsync(messageId);
            await progressSink.OnTextDeltaAsync(messageId, $"Turn {turnNumber}");
            await progressSink.OnReasoningAsync(reasoningId, $"Reasoning turn {turnNumber}");
            await progressSink.OnTextDeltaAsync(messageId, " reply");
            await progressSink.CompleteAsync();

            return
            (
                [],
                [
                    new ChatMessage(
                        ChatRole.Assistant,
                        [
                            new TextContent($"Turn {turnNumber} reply"),
                            new TextReasoningContent($"Reasoning turn {turnNumber}"),
                        ]
                    ),
                ],
                $"Turn {turnNumber} reply"
            );
        }
    }

    private sealed class GoogleStreamingTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            await progressSink.OnTextStartAsync("google-assistant-1");
            await progressSink.OnTextDeltaAsync("google-assistant-1", "Google");
            await progressSink.OnReasoningAsync("google-assistant-1", "Google reasoning");
            await progressSink.OnTextDeltaAsync("google-assistant-1", " reply");
            await progressSink.OnReasoningAsync("google-assistant-1", "Google reasoning final");
            await progressSink.OnToolCallAsync("google-call-1", "google_lookup", "{\"query\":\"weather\"}", "google-assistant-1");
            await progressSink.OnToolCallResultAsync("google-lookup-result-1", "google-call-1", "Sunny");
            await progressSink.CompleteAsync();

            IList<ChatMessage> responses =
            [
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new TextContent("Google reply"),
                        new TextReasoningContent("Google reasoning final"),
                        new FunctionCallContent("google-call-1", "google_lookup", new Dictionary<string, object?>() { ["query"] = "weather" }),
                        new FunctionResultContent("google-call-1", "Sunny"),
                    ]
                ),
            ];

            return ((IList<ChatMessage>)new List<ChatMessage>(), responses, "Google reply");
        }
    }

    private sealed class BatchFallbackTestModelCaller : IModelCaller
    {
        public Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            IList<ChatMessage> responses =
            [
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new TextContent("Batch"),
                        new TextReasoningContent("Batch reasoning"),
                        new TextContent(" reply"),
                        new FunctionCallContent("batch-call-1", "batch_lookup", new Dictionary<string, object?>() { ["city"] = "Melbourne" }),
                        new FunctionResultContent("batch-call-1", "Batch tool result"),
                    ]
                ),
            ];

            return Task.FromResult(((IList<ChatMessage>)new List<ChatMessage>(), responses, (object?)"Batch reply"));
        }
    }

    private sealed class ReasoningThenToolCallTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            await progressSink.OnReasoningAsync("assistant-1", "Thinking");
            await progressSink.OnToolCallAsync("call-1", "lookup_weather", "{\"city\":\"Sydney\"}", "assistant-1");
            await progressSink.CompleteAsync();
            return ([], [], string.Empty);
        }
    }

    private sealed class TextThenToolCallTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            await progressSink.OnTextStartAsync("assistant-1");
            await progressSink.OnTextDeltaAsync("assistant-1", "Hello");
            await progressSink.OnToolCallAsync("call-1", "lookup_weather", "{\"city\":\"Sydney\"}", "assistant-1");
            await progressSink.CompleteAsync();
            return ([], [], "Hello");
        }
    }

    private sealed class AssistantTextOnlyTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            await progressSink.OnTextStartAsync("assistant-1");
            await progressSink.OnTextDeltaAsync("assistant-1", "Hello");
            await progressSink.OnTextDeltaAsync("assistant-1", " world");
            await progressSink.CompleteAsync();
            return ([], [], "Hello world");
        }
    }

    private sealed class WhitespaceAssistantTextTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            await progressSink.OnTextDeltaAsync("assistant-1", "   ");
            await progressSink.CompleteAsync();
            return ([], [], string.Empty);
        }
    }

    private sealed class EmptyReasoningAfterToolCallTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            await progressSink.OnReasoningAsync("assistant-1", "Thinking");
            await progressSink.OnToolCallAsync("call-1", "lookup_weather", "{\"city\":\"Sydney\"}", "assistant-1");
            await progressSink.OnReasoningAsync("assistant-1", string.Empty);
            await progressSink.CompleteAsync();
            return ([], [], string.Empty);
        }
    }

    private sealed class EmptyReasoningOnlyTestModelCaller : IModelCaller
    {
        public async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            await progressSink.OnReasoningAsync("assistant-1", string.Empty);
            await progressSink.CompleteAsync();
            return ([], [], string.Empty);
        }
    }

    private sealed class CapturingProgressService : IProgressService
    {
        public List<RunStatus> InformationStatuses { get; } = [];
        public List<RunStatus> StreamEventStatuses { get; } = [];
        public List<StreamEventKind> StreamEventKinds { get; } = [];
        public List<bool> StreamEventSilentFlags { get; } = [];
        public List<string> InformationLog { get; } = [];
        public List<string> ProgressLog { get; } = [];

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
            if (informations.Count > 0)
            {
                InformationStatuses.Add(run.RunStatus);
                InformationLog.AddRange(informations.Select(i => $"{i.InformationType}:{i.Text}"));
                ProgressLog.AddRange(informations.Select(i => $"info:{i.InformationType}"));
            }

            return Task.CompletedTask;
        }

        public Task StreamEventProgress(Run run, List<StreamEventProgressItem> events)
        {
            if (events.Count > 0)
            {
                StreamEventStatuses.Add(run.RunStatus);
                StreamEventKinds.AddRange(events.Select(e => e.Event.EventKind));
                StreamEventSilentFlags.AddRange(events.Select(e => e.Silent));
                ProgressLog.AddRange(events.Select(e => $"stream:{e.Event.EventKind}"));
            }

            return Task.CompletedTask;
        }

        public Task EvalRunProgress(EvalRun evalRun)
        {
            return Task.CompletedTask;
        }
    }
}
