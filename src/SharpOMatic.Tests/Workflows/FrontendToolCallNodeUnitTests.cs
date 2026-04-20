namespace SharpOMatic.Tests.Workflows;

public sealed class FrontendToolCallNodeUnitTests
{
    [Fact]
    public async Task Frontend_tool_call_matches_tool_result_and_keeps_call_and_result_when_requested()
    {
        var workflow = CreateBranchingWorkflow(
            chatPersistenceMode: ToolCallChatPersistenceMode.FunctionCallAndResult,
            hideFromReplyAfterHandled: true
        );

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();
        const string streamConversationId = "frontend-tool-call-handled";

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );
            Assert.True(firstTurn.RunStatus == RunStatus.Suspended, firstTurn.Error);
            await WaitForConversationStatusAsync(repositoryService, conversationId, ConversationStatus.Suspended);

            var pendingContext = await GetCheckpointContext(repositoryService, conversationId, jsonConverters.GetConverters());
            var toolCallId = pendingContext.Get<string>("_frontendToolCall.toolCallId");

            var secondTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                CreateResumeInput(
                    pendingContext,
                    CreateToolResultIncomingMessage("tool-message-1", toolCallId, """{"approved":true,"reason":"ok"}""")
                ),
                streamConversationId: streamConversationId
            );

            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("toolResult", output.Get<string>("output.route"));
            Assert.False(output.TryGetObject("_frontendToolCall", out _));

            var parsedResult = output.Get<ContextObject>("output.toolResult");
            Assert.True(parsedResult.Get<bool>("approved"));
            Assert.Equal("ok", parsedResult.Get<string>("reason"));

            var chatHistory = output.Get<ContextList>("input.chat");
            Assert.Equal(2, chatHistory.Count);
            Assert.All(chatHistory, item => Assert.IsType<ChatMessage>(item));
            Assert.Equal(ChatRole.Assistant, ((ChatMessage)chatHistory[0]!).Role);
            Assert.Equal(ChatRole.Tool, ((ChatMessage)chatHistory[1]!).Role);

            var replayEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            Assert.Empty(replayEvents);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Frontend_tool_call_keeps_only_function_call_when_configured()
    {
        var workflow = CreateBranchingWorkflow(chatPersistenceMode: ToolCallChatPersistenceMode.FunctionCallOnly);

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);
            await WaitForConversationStatusAsync(repositoryService, conversationId, ConversationStatus.Suspended);

            var pendingContext = await GetCheckpointContext(repositoryService, conversationId, jsonConverters.GetConverters());
            var toolCallId = pendingContext.Get<string>("_frontendToolCall.toolCallId");

            var secondTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                CreateResumeInput(
                    pendingContext,
                    CreateToolResultIncomingMessage("tool-message-1", toolCallId, """{"approved":true}""")
                )
            );

            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.False(output.TryGetObject("_frontendToolCall", out _));
            var chatHistory = output.Get<ContextList>("input.chat");
            Assert.Single(chatHistory);
            Assert.Equal(ChatRole.Assistant, ((ChatMessage)chatHistory[0]!).Role);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Frontend_tool_call_removes_tool_messages_from_chat_when_persistence_is_none()
    {
        var workflow = CreateBranchingWorkflow(chatPersistenceMode: ToolCallChatPersistenceMode.None);

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);
            await WaitForConversationStatusAsync(repositoryService, conversationId, ConversationStatus.Suspended);

            var pendingContext = await GetCheckpointContext(repositoryService, conversationId, jsonConverters.GetConverters());
            var toolCallId = pendingContext.Get<string>("_frontendToolCall.toolCallId");

            var secondTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                CreateResumeInput(
                    pendingContext,
                    CreateToolResultIncomingMessage("tool-message-1", toolCallId, """{"approved":true}""")
                )
            );

            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.False(output.TryGetObject("_frontendToolCall", out _));
            Assert.True(output.TryGet<ContextList>("input.chat", out var chatHistory));
            Assert.NotNull(chatHistory);
            Assert.Empty(chatHistory);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Frontend_tool_call_other_input_cleans_up_chat_and_hides_tool_call_events()
    {
        var workflow = CreateBranchingWorkflow(
            chatPersistenceMode: ToolCallChatPersistenceMode.FunctionCallAndResult
        );

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();
        const string streamConversationId = "frontend-tool-call-other";

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

            var pendingContext = await GetCheckpointContext(repositoryService, conversationId, jsonConverters.GetConverters());

            var secondTurn = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                CreateResumeInput(
                    pendingContext,
                    CreateUserIncomingMessage("user-message-1", "No thanks")
                ),
                streamConversationId: streamConversationId
            );

            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("otherInput", output.Get<string>("output.route"));
            Assert.False(output.TryGetObject("_frontendToolCall", out _));

            var chatHistory = output.Get<ContextList>("input.chat");
            Assert.Single(chatHistory);
            var remainingMessage = Assert.IsType<ChatMessage>(chatHistory[0]);
            Assert.Equal(ChatRole.User, remainingMessage.Role);

            var replayEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            Assert.Empty(replayEvents);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Frontend_tool_call_cleans_up_pending_state_when_tool_result_processing_fails()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddEdit("seed", WorkflowBuilder.CreateStringUpsert("output", "already-a-string"))
            .AddFrontendToolCall(
                title: "call",
                functionName: "request_confirmation",
                argumentsMode: ToolCallDataMode.FixedJson,
                argumentsJson: """{"question":"Proceed?"}""",
                resultOutputPath: "output.toolResult",
                chatPersistenceMode: ToolCallChatPersistenceMode.FunctionCallAndResult
            )
            .Connect("start", "seed")
            .Connect("seed", "call")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);
            await WaitForConversationStatusAsync(repositoryService, conversationId, ConversationStatus.Suspended);

            var pendingContext = await GetCheckpointContext(repositoryService, conversationId, jsonConverters.GetConverters());
            var toolCallId = pendingContext.Get<string>("_frontendToolCall.toolCallId");

            var failedRun = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                CreateResumeInput(
                    pendingContext,
                    CreateToolResultIncomingMessage("tool-message-1", toolCallId, """{"approved":true}""")
                )
            );

            Assert.Equal(RunStatus.Failed, failedRun.RunStatus);
            Assert.Equal("Could not set 'output.toolResult' into context.", failedRun.Error);

            var output = ContextObject.Deserialize(failedRun.OutputContext, jsonConverters.GetConverters());
            Assert.False(output.TryGetObject("_frontendToolCall", out _));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Frontend_tool_call_fails_without_conversation()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFrontendToolCall()
            .Connect("start", "FE Tool Call")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Node suspension requires an active conversation.", run.Error);
    }

    [Fact]
    public async Task Frontend_tool_call_resume_requires_exactly_one_message()
    {
        await AssertResumeMessageCountFailure(
            [],
            "Frontend Tool Call requires exactly one incoming message on resume but received 0."
        );

        await AssertResumeMessageCountFailure(
            [
                CreateUserIncomingMessage("user-message-1", "one"),
                CreateUserIncomingMessage("user-message-2", "two")
            ],
            "Frontend Tool Call requires exactly one incoming message on resume but received 2."
        );
    }

    [Fact]
    public async Task Frontend_tool_call_rejects_invalid_fixed_json()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddFrontendToolCall(argumentsMode: ToolCallDataMode.FixedJson, argumentsJson: """{"broken":""")
            .Connect("start", "FE Tool Call")
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

            Assert.Equal(RunStatus.Failed, run.RunStatus);
            Assert.Equal("Frontend Tool Call fixed arguments must contain valid JSON.", run.Error);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Frontend_tool_call_serializes_context_path_arguments()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddEdit("seed", WorkflowBuilder.CreateJsonUpsert("input.payload", json: """{"approved":true,"count":2}"""))
            .AddFrontendToolCall(
                argumentsMode: ToolCallDataMode.ContextPath,
                argumentsPath: "input.payload",
                hideFromReplyAfterHandled: false
            )
            .Connect("start", "seed")
            .Connect("seed", "FE Tool Call")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();
        const string streamConversationId = "frontend-tool-call-args";

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                streamConversationId: streamConversationId
            );

            Assert.True(run.RunStatus == RunStatus.Suspended, run.Error);

            var streamEvents = await repositoryService.GetConversationStreamEvents(streamConversationId);
            var argsEvent = Assert.Single(streamEvents, e => e.EventKind == StreamEventKind.ToolCallArgs);
            AssertJsonEqual("""{"approved":true,"count":2}""", argsEvent.TextDelta!);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static WorkflowEntity CreateBranchingWorkflow(
        ToolCallChatPersistenceMode chatPersistenceMode,
        bool hideFromReplyAfterHandled = false
    )
    {
        return new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddFrontendToolCall(
                title: "call",
                functionName: "request_confirmation",
                argumentsMode: ToolCallDataMode.FixedJson,
                argumentsJson: """{"question":"Proceed?"}""",
                resultOutputPath: "output.toolResult",
                chatPersistenceMode: chatPersistenceMode,
                hideFromReplyAfterHandled: hideFromReplyAfterHandled
            )
            .AddCode("toolBranch", """Context.Set<string>("output.route", "toolResult");""")
            .AddCode("otherBranch", """Context.Set<string>("output.route", "otherInput");""")
            .AddEnd()
            .Connect("start", "call")
            .Connect("call.toolResult", "toolBranch")
            .Connect("call.otherInput", "otherBranch")
            .Connect("toolBranch", "end")
            .Connect("otherBranch", "end")
            .Build();
    }

    private static async Task AssertResumeMessageCountFailure(
        IReadOnlyList<IncomingMessageSpec> incomingMessages,
        string expectedError
    )
    {
        var workflow = CreateBranchingWorkflow(ToolCallChatPersistenceMode.FunctionCallAndResult);

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);
            await WaitForConversationStatusAsync(repositoryService, conversationId, ConversationStatus.Suspended);

            var pendingContext = await GetCheckpointContext(repositoryService, conversationId, jsonConverters.GetConverters());
            var failedRun = await engineService.StartOrResumeConversationAndWait(
                workflow.Id,
                conversationId,
                CreateResumeInput(pendingContext, incomingMessages.ToArray())
            );

            Assert.Equal(RunStatus.Failed, failedRun.RunStatus);
            Assert.Equal(expectedError, failedRun.Error);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static ContextMergeResumeInput CreateResumeInput(ContextObject checkpointContext, params IncomingMessageSpec[] incomingMessages)
    {
        ContextObject resumeContext = [];

        ContextObject agent = [];
        ContextList agentMessages = [];
        foreach (var incomingMessage in incomingMessages)
            agentMessages.Add(incomingMessage.AgentMessage);

        agent["messages"] = agentMessages;
        resumeContext["agent"] = agent;

        ContextList mergedChat = [];
        if (checkpointContext.TryGet<ContextList>("input.chat", out var existingChat) && existingChat is not null)
        {
            foreach (var chatEntry in existingChat)
                mergedChat.Add(chatEntry);
        }

        foreach (var incomingMessage in incomingMessages)
            mergedChat.Add(incomingMessage.ChatMessage);

        resumeContext.Set("input.chat", mergedChat);
        return new ContextMergeResumeInput() { Context = resumeContext };
    }

    private static IncomingMessageSpec CreateToolResultIncomingMessage(string messageId, string toolCallId, string content)
    {
        ContextObject agentMessage = [];
        agentMessage["id"] = messageId;
        agentMessage["role"] = "tool";
        agentMessage["toolCallId"] = toolCallId;
        agentMessage["content"] = content;

        var chatMessage = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(toolCallId, content)])
        {
            MessageId = messageId,
        };

        return new IncomingMessageSpec(agentMessage, chatMessage);
    }

    private static IncomingMessageSpec CreateUserIncomingMessage(string messageId, string content)
    {
        ContextObject agentMessage = [];
        agentMessage["id"] = messageId;
        agentMessage["role"] = "user";
        agentMessage["content"] = content;

        var chatMessage = new ChatMessage(ChatRole.User, [new TextContent(content)])
        {
            MessageId = messageId,
        };

        return new IncomingMessageSpec(agentMessage, chatMessage);
    }

    private static Guid GetNodeId(WorkflowEntity workflow, string title)
    {
        return workflow.Nodes.Single(node => string.Equals(node.Title, title, StringComparison.Ordinal)).Id;
    }

    private static async Task<ContextObject> GetCheckpointContext(IRepositoryService repositoryService, string conversationId, IEnumerable<JsonConverter> converters)
    {
        var checkpoint = await repositoryService.GetConversationCheckpoint(conversationId);
        Assert.NotNull(checkpoint);
        return ContextObject.Deserialize(checkpoint!.ContextJson, converters);
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

    private static void AssertJsonEqual(string expectedJson, string actualJson)
    {
        using var expected = JsonDocument.Parse(expectedJson);
        using var actual = JsonDocument.Parse(actualJson);
        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual.RootElement), $"Expected JSON '{expectedJson}' but received '{actualJson}'.");
    }

    private static string NewConversationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private sealed record IncomingMessageSpec(ContextObject AgentMessage, ChatMessage ChatMessage);
}
