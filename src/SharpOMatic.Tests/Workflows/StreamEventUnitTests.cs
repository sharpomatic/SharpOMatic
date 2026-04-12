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
}

