namespace SharpOMatic.Tests.Workflows;

public sealed class SuspendNodeUnitTests
{
    [Fact]
    public async Task Suspend_suspends_and_resumes_conversation()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddSuspend("ask")
            .AddCode("code", "Context.Set<string>(\"output.answer\", Context.Get<string>(\"resume.answer\"));")
            .AddEnd()
            .Connect("start", "ask")
            .Connect("ask", "code")
            .Connect("code", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var conversations = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var repository = scope.ServiceProvider.GetRequiredService<IRepositoryService>();

            var firstTurn = await conversations.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);

            var conversation = await repository.GetConversation(conversationId);
            Assert.NotNull(conversation);
            Assert.Equal(ConversationStatus.Suspended, conversation!.Status);

            var secondTurn = await conversations.StartOrResumeConversationAndWait(workflow.Id, conversationId, CreateContextResumeInput("resume.answer", "final answer"));
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);
            Assert.NotNull(secondTurn.Started);
            Assert.NotNull(secondTurn.Stopped);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("final answer", output.Get<string>("output.answer"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Suspend_conversation_can_start_and_notify()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddSuspend("ask")
            .AddEnd()
            .Connect("start", "ask")
            .Connect("ask", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var runId = await engineService.StartOrResumeConversationAndNotify(workflow.Id, conversationId);
            Assert.NotEqual(Guid.Empty, runId);

            var run = await WaitForConversationRun(repositoryService, conversationId, RunStatus.Suspended);
            Assert.Equal(RunStatus.Suspended, run.RunStatus);

            var conversation = await repositoryService.GetConversation(conversationId);
            Assert.NotNull(conversation);
            Assert.Equal(ConversationStatus.Suspended, conversation!.Status);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_can_start_with_input_entries()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("code", "Context.Set<string>(\"output.value\", Context.Get<string>(\"input.value\"));")
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
            var inputEntries = WorkflowBuilder.CreateContextEntryList(
                WorkflowBuilder.CreateStringInput("input.value", entryValue: "from input entry")
            );

            var run = await engineService.StartOrResumeConversationAndWait(workflow.Id, NewConversationId(), inputEntries: inputEntries);
            Assert.Equal(RunStatus.Success, run.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("from input entry", output.Get<string>("output.value"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_start_can_apply_agui_agent_resume_input()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("code", """
                Context.Set<string>("output.userMessage", Context.Get<string>("agent.latestUserMessage.content"));
                Context.Set<string>("output.mode", Context.Get<string>("agent.state.mode"));
                Context.Set<string>("output.contextId", Context.Get<string>("agent.context[0].id"));
                """)
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

            var run = await engineService.StartOrResumeConversationAndWait(workflow.Id, NewConversationId(), CreateAgUiAgentResumeInput(
                ("latestUserMessage.content", "hello"),
                ("state.mode", "assistant"),
                ("context[0].id", "ctx-1")
            ));
            Assert.Equal(RunStatus.Success, run.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("hello", output.Get<string>("output.userMessage"));
            Assert.Equal("assistant", output.Get<string>("output.mode"));
            Assert.Equal("ctx-1", output.Get<string>("output.contextId"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_can_start_synchronously()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("code", "Context.Set<string>(\"output.value\", \"done\");")
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
            using var scope = provider.CreateScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var run = engineService.StartOrResumeConversationSynchronously(workflow.Id, NewConversationId());
            Assert.Equal(RunStatus.Success, run.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("done", output.Get<string>("output.value"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Completed_conversation_uses_latest_workflow_on_next_turn()
    {
        var workflowId = Guid.NewGuid();
        var workflowV1 = new WorkflowBuilder()
            .WithId(workflowId)
            .EnableConversations()
            .AddStart()
            .AddCode("code", "Context.Set<string>(\"output.version\", \"one\");")
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflowV1);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var conversations = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await conversations.StartOrResumeConversationAndWait(workflowId, conversationId);
            Assert.Equal(RunStatus.Success, firstTurn.RunStatus);

            var workflowV2 = new WorkflowBuilder()
                .WithId(workflowId)
                .EnableConversations()
                .AddStart()
                .AddCode("code", "Context.Set<string>(\"output.version\", \"two\");")
                .AddEnd()
                .Connect("start", "code")
                .Connect("code", "end")
                .Build();
            await repositoryService.UpsertWorkflow(workflowV2);

            var secondTurn = await conversations.StartOrResumeConversationAndWait(workflowId, conversationId);
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("two", output.Get<string>("output.version"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_asset_can_be_resolved_by_name_across_turns()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("create", """
                var created = await Assets.AddAssetFromBytesAsync(System.Text.Encoding.UTF8.GetBytes("hello"), "conversation-file.txt", "text/plain", AssetScope.Conversation);
                Context.Set<AssetRef>("output.createdAsset", created);
                """)
            .AddSuspend("ask")
            .AddCode("read", """
                var stored = Context.Get<AssetRef>("output.createdAsset");
                var byName = await Assets.GetAssetRefAsync("conversation-file.txt");
                Context.Set<Guid>("output.storedAssetId", stored.AssetId);
                Context.Set<Guid>("output.byNameAssetId", byName.AssetId);
                """)
            .AddEnd()
            .Connect("start", "create")
            .Connect("create", "ask")
            .Connect("ask", "read")
            .Connect("read", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
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

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.Equal(output.Get<Guid>("output.storedAssetId"), output.Get<Guid>("output.byNameAssetId"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_history_prunes_whole_conversations_but_run_pruning_only_prunes_non_conversation_runs()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("code", "Context.Set<string>(\"output.value\", \"done\");")
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        var regularWorkflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "Context.Set<string>(\"output.value\", \"done\");")
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);
        await repositoryService.UpsertWorkflow(regularWorkflow);
        await repositoryService.UpsertSetting(new Setting()
        {
            SettingId = Guid.NewGuid(),
            Name = "ConversationHistoryLimit",
            DisplayName = "Conversation History Limit",
            SettingType = SettingType.Integer,
            UserEditable = true,
            ValueInteger = 1
        });
        await repositoryService.UpsertSetting(new Setting()
        {
            SettingId = Guid.NewGuid(),
            Name = "RunHistoryLimit",
            DisplayName = "Run History Limit",
            SettingType = SettingType.Integer,
            UserEditable = true,
            ValueInteger = 1
        });

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstConversationId = NewConversationId();
            var secondConversationId = NewConversationId();

            var firstConversationRun = await engineService.StartOrResumeConversationAndWait(workflow.Id, firstConversationId);
            var secondConversationRun = await engineService.StartOrResumeConversationAndWait(workflow.Id, secondConversationId);
            Assert.Equal(RunStatus.Success, firstConversationRun.RunStatus);
            Assert.Equal(RunStatus.Success, secondConversationRun.RunStatus);

            await repositoryService.PruneWorkflowConversations(workflow.Id, 1);

            var firstConversation = await repositoryService.GetConversation(firstConversationId);
            var secondConversation = await repositoryService.GetConversation(secondConversationId);
            Assert.True((firstConversation is null) ^ (secondConversation is null));

            if (firstConversation is null)
            {
                Assert.Empty(await repositoryService.GetConversationRuns(firstConversationId));
                Assert.Single(await repositoryService.GetConversationRuns(secondConversationId));
            }
            else
            {
                Assert.Single(await repositoryService.GetConversationRuns(firstConversationId));
                Assert.Empty(await repositoryService.GetConversationRuns(secondConversationId));
            }

            await engineService.StartWorkflowRunAndWait(regularWorkflow.Id);
            await engineService.StartWorkflowRunAndWait(regularWorkflow.Id);
            await repositoryService.PruneWorkflowRuns(regularWorkflow.Id, 1);

            var regularRuns = await repositoryService.GetWorkflowRuns(regularWorkflow.Id, RunSortField.Created, SortDirection.Descending, 0, 10);
            Assert.Single(regularRuns);
            Assert.All(regularRuns, run => Assert.Null(run.ConversationId));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Workflow_returns_latest_conversation_by_updated()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddEnd()
            .Connect("start", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        var firstConversation = new Conversation()
        {
            ConversationId = NewConversationId(),
            WorkflowId = workflow.Id,
            Status = ConversationStatus.Completed,
            Created = DateTime.UtcNow.AddMinutes(-10),
            Updated = DateTime.UtcNow.AddMinutes(-5),
            CurrentTurnNumber = 1,
        };
        var secondConversation = new Conversation()
        {
            ConversationId = NewConversationId(),
            WorkflowId = workflow.Id,
            Status = ConversationStatus.Completed,
            Created = DateTime.UtcNow.AddMinutes(-9),
            Updated = DateTime.UtcNow,
            CurrentTurnNumber = 2,
        };

        await repositoryService.UpsertConversation(firstConversation);
        await repositoryService.UpsertConversation(secondConversation);

        var latestConversation = await repositoryService.GetLatestConversationForWorkflow(workflow.Id);
        Assert.NotNull(latestConversation);
        Assert.Equal(secondConversation.ConversationId, latestConversation!.ConversationId);
    }

    [Fact]
    public async Task Gosub_suspend_suspends_and_resumes_conversation()
    {
        var childWorkflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddSuspend("child-suspend")
            .AddCode("child-code", "Context.Set<string>(\"output.answer\", Context.Get<string>(\"resume.answer\"));")
            .AddEnd()
            .Connect("start", "child-suspend")
            .Connect("child-suspend", "child-code")
            .Connect("child-code", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddGosub("child", workflowId: childWorkflow.Id)
            .AddEnd()
            .Connect("start", "child")
            .Connect("child", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(parentWorkflow);
        await repositoryService.UpsertWorkflow(childWorkflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var conversations = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await conversations.StartOrResumeConversationAndWait(parentWorkflow.Id, conversationId);
            Assert.Equal(RunStatus.Suspended, firstTurn.RunStatus);

            var secondTurn = await conversations.StartOrResumeConversationAndWait(parentWorkflow.Id, conversationId, CreateContextResumeInput("resume.answer", "child answer"));
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("child answer", output.Get<string>("output.answer"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Suspend_resume_with_agui_agent_input_overwrites_existing_agent_context()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("setup", """
                Context.Set<string>("agent.latestUserMessage.id", "user-1");
                Context.Set<string>("agent.latestUserMessage.content", "before");
                Context.Set<string>("agent.latestToolResult.content", "old tool result");
                Context.Set<string>("agent.state.keep", "existing");
                Context.Set<string>("agent.context.old", "value");
                """)
            .AddSuspend("ask")
            .AddCode("after", """
                Context.Set<string>("output.messageContent", Context.Get<string>("agent.latestUserMessage.content"));
                Context.Set<string>("output.newValue", Context.Get<string>("agent.state.newValue"));
                Context.Set<bool>("output.hasMessageId", Context.TryGet<string>("agent.latestUserMessage.id", out _));
                Context.Set<bool>("output.hasLatestToolResult", Context.TryGet<string>("agent.latestToolResult.content", out _));
                Context.Set<bool>("output.hasKeep", Context.TryGet<string>("agent.state.keep", out _));
                Context.Set<bool>("output.hasOldContext", Context.TryGet<string>("agent.context.old", out _));
                """)
            .AddEnd()
            .Connect("start", "setup")
            .Connect("setup", "ask")
            .Connect("ask", "after")
            .Connect("after", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
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

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, CreateAgUiAgentResumeInput(
                ("latestUserMessage.content", "after"),
                ("state.newValue", "fresh")
            ));
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("after", output.Get<string>("output.messageContent"));
            Assert.Equal("fresh", output.Get<string>("output.newValue"));
            Assert.False(output.Get<bool>("output.hasMessageId"));
            Assert.False(output.Get<bool>("output.hasLatestToolResult"));
            Assert.False(output.Get<bool>("output.hasKeep"));
            Assert.False(output.Get<bool>("output.hasOldContext"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Completed_conversation_agui_agent_input_replaces_agent_context_atomically_on_next_turn()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("inspect", """
                Context.Set<bool>("output.hasLatestUserMessage", Context.TryGet<string>("agent.latestUserMessage.content", out _));
                Context.Set<bool>("output.hasLatestToolResult", Context.TryGet<string>("agent.latestToolResult.content", out var toolResult));
                if (toolResult is not null)
                    Context.Set<string>("output.toolResult", toolResult);
                """)
            .AddEnd()
            .Connect("start", "inspect")
            .Connect("inspect", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, CreateAgUiAgentResumeInput(
                ("latestUserMessage.content", "first prompt")
            ));
            Assert.Equal(RunStatus.Success, firstTurn.RunStatus);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, CreateAgUiAgentResumeInput(
                ("latestToolResult.content", "tool result"),
                ("latestToolResult.toolCallId", "call-1")
            ));
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.False(output.Get<bool>("output.hasLatestUserMessage"));
            Assert.True(output.Get<bool>("output.hasLatestToolResult"));
            Assert.Equal("tool result", output.Get<string>("output.toolResult"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Suspend_resume_with_context_merge_input_merges_agent_context_recursively()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("setup", """
                Context.Set<string>("agent.latestToolResult.content", "old tool result");
                Context.Set<string>("agent.latestToolResult.toolCallId", "old-call");
                Context.Set<string>("agent.state.old", "old state");
                Context.Set<string>("settings.keep", "checkpoint");
                """)
            .AddSuspend("ask")
            .AddCode("after", """
                Context.Set<string>("output.userMessage", Context.Get<string>("agent.latestUserMessage.content"));
                Context.Set<string>("output.keep", Context.Get<string>("settings.keep"));
                Context.Set<string>("output.added", Context.Get<string>("settings.added"));
                Context.Set<bool>("output.hasLatestToolResult", Context.TryGet<string>("agent.latestToolResult.content", out _));
                Context.Set<bool>("output.hasOldAgentState", Context.TryGet<string>("agent.state.old", out _));
                """)
            .AddEnd()
            .Connect("start", "setup")
            .Connect("setup", "ask")
            .Connect("ask", "after")
            .Connect("after", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
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

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, CreateContextResumeInput(
                ("agent.latestUserMessage.content", "new prompt"),
                ("settings.added", "fresh")
            ));
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var input = ContextObject.Deserialize(secondTurn.InputContext, jsonConverters.GetConverters());
            Assert.Equal("new prompt", input.Get<string>("agent.latestUserMessage.content"));
            Assert.Equal("old tool result", input.Get<string>("agent.latestToolResult.content"));
            Assert.Equal("old state", input.Get<string>("agent.state.old"));
            Assert.Equal("checkpoint", input.Get<string>("settings.keep"));
            Assert.Equal("fresh", input.Get<string>("settings.added"));
            Assert.False(input.TryGet<string>("output.userMessage", out _));

            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("new prompt", output.Get<string>("output.userMessage"));
            Assert.Equal("checkpoint", output.Get<string>("output.keep"));
            Assert.Equal("fresh", output.Get<string>("output.added"));
            Assert.True(output.Get<bool>("output.hasLatestToolResult"));
            Assert.True(output.Get<bool>("output.hasOldAgentState"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Completed_conversation_context_merge_input_merges_agent_context_recursively_on_next_turn()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("inspect", """
                Context.Set<bool>("output.hasLatestUserMessage", Context.TryGet<string>("agent.latestUserMessage.content", out _));
                Context.Set<bool>("output.hasLatestToolResult", Context.TryGet<string>("agent.latestToolResult.content", out var toolResult));
                if (toolResult is not null)
                    Context.Set<string>("output.toolResult", toolResult);

                Context.Set<string>("output.keep", Context.Get<string>("settings.keep"));
                if (Context.TryGet<string>("settings.added", out var added))
                    Context.Set<string>("output.added", added);
                """)
            .AddEnd()
            .Connect("start", "inspect")
            .Connect("inspect", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, CreateContextResumeInput(
                ("agent.latestUserMessage.content", "first prompt"),
                ("settings.keep", "checkpoint")
            ));
            Assert.Equal(RunStatus.Success, firstTurn.RunStatus);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, CreateContextResumeInput(
                ("agent.latestToolResult.content", "tool result"),
                ("agent.latestToolResult.toolCallId", "call-1"),
                ("settings.added", "fresh")
            ));
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
            var output = ContextObject.Deserialize(secondTurn.OutputContext, jsonConverters.GetConverters());
            Assert.True(output.Get<bool>("output.hasLatestUserMessage"));
            Assert.True(output.Get<bool>("output.hasLatestToolResult"));
            Assert.Equal("tool result", output.Get<string>("output.toolResult"));
            Assert.Equal("checkpoint", output.Get<string>("output.keep"));
            Assert.Equal("fresh", output.Get<string>("output.added"));
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_requires_enabled_workflow()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEnd().Connect("start", "end").Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        await using var scope = provider.CreateAsyncScope();
        var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            engineService.StartOrResumeConversationAndWait(workflow.Id, NewConversationId())
        );
        Assert.Equal("Workflow is not enabled for conversations.", exception.Message);
    }

    [Fact]
    public async Task Conversation_enabled_workflow_cannot_wait_regular_run()
    {
        var workflow = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        await using var scope = provider.CreateAsyncScope();
        var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            engineService.StartWorkflowRunAndWait(workflow.Id)
        );
        Assert.Equal("Conversation-enabled workflows must be started through conversation APIs.", exception.Message);
    }

    [Fact]
    public async Task Conversation_enabled_workflow_cannot_notify_regular_run()
    {
        var workflow = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        await using var scope = provider.CreateAsyncScope();
        var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

        var notifyException = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            engineService.StartWorkflowRunAndNotify(workflow.Id)
        );
        Assert.Equal("Conversation-enabled workflows must be started through conversation APIs.", notifyException.Message);
    }

    [Fact]
    public async Task Conversation_enabled_workflow_cannot_start_regular_run_synchronously()
    {
        var workflow = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var scope = provider.CreateScope();
        var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

        var exception = Assert.Throws<SharpOMaticException>(() =>
            engineService.StartWorkflowRunSynchronously(workflow.Id)
        );
        Assert.Equal("Conversation-enabled workflows must be started through conversation APIs.", exception.Message);
    }

    [Fact]
    public async Task Conversation_id_cannot_be_reused_for_different_workflow()
    {
        var workflow1 = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();
        var workflow2 = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();
        var conversationId = NewConversationId();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow1);
        await repositoryService.UpsertWorkflow(workflow2);
        await repositoryService.UpsertConversation(
            new Conversation()
            {
                ConversationId = conversationId,
                WorkflowId = workflow1.Id,
                Status = ConversationStatus.Created,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                CurrentTurnNumber = 0,
            }
        );

        await using var scope = provider.CreateAsyncScope();
        var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            engineService.StartOrResumeConversationAndWait(workflow2.Id, conversationId)
        );
        Assert.Equal("Conversation id does not belong to the requested workflow.", exception.Message);
    }

    [Fact]
    public async Task Conversation_running_lease_conflict_fails()
    {
        var workflow = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();
        var conversationId = NewConversationId();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);
        await repositoryService.UpsertConversation(
            new Conversation()
            {
                ConversationId = conversationId,
                WorkflowId = workflow.Id,
                Status = ConversationStatus.Created,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                CurrentTurnNumber = 0,
            }
        );

        var leaseTaken = await repositoryService.TryAcquireConversationLease(conversationId, "lease-owner", DateTime.UtcNow.AddMinutes(5));
        Assert.True(leaseTaken);

        await using var scope = provider.CreateAsyncScope();
        var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId)
        );
        Assert.Equal("Conversation is already running.", exception.Message);
    }

    [Fact]
    public async Task Suspended_conversation_requires_checkpoint()
    {
        var workflow = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();
        var conversationId = NewConversationId();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);
        await repositoryService.UpsertConversation(
            new Conversation()
            {
                ConversationId = conversationId,
                WorkflowId = workflow.Id,
                Status = ConversationStatus.Suspended,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                CurrentTurnNumber = 1,
            }
        );

        await using var scope = provider.CreateAsyncScope();
        var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId)
        );
        Assert.Equal("Conversation is suspended but has no checkpoint.", exception.Message);

        var conversation = await repositoryService.GetConversation(conversationId);
        Assert.NotNull(conversation);
        Assert.Null(conversation!.LeaseOwner);
        Assert.Null(conversation.LeaseExpires);
    }

    [Fact]
    public async Task Conversation_cannot_suspend_inside_batch_scope()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddCode("setup", "Context.Set<ContextList>(\"items\", new ContextList() { 1, 2 });")
            .AddBatch(inputPath: "items")
            .AddSuspend("ask")
            .Connect("start", "setup")
            .Connect("setup", "batch")
            .Connect("batch.process", "ask")
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
            Assert.Contains("Conversation suspension is only supported in the root workflow and gosub scopes.", run.Error);

            var conversation = await repositoryService.GetConversation(run.ConversationId!);
            Assert.NotNull(conversation);
            Assert.Equal(ConversationStatus.Created, conversation!.Status);
            Assert.Equal(run.Error, conversation.LastError);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_start_rejects_unknown_resume_input()
    {
        var workflow = new WorkflowBuilder().EnableConversations().AddStart().AddEnd().Connect("start", "end").Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        await using var scope = provider.CreateAsyncScope();
        var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            engineService.StartOrResumeConversationAndWait(workflow.Id, NewConversationId(), new UnknownResumeInput())
        );
        Assert.Equal("Conversation start cannot handle resume input type 'UnknownResumeInput'.", exception.Message);
    }

    [Fact]
    public async Task Suspended_conversation_rejects_unknown_resume_input()
    {
        var workflow = new WorkflowBuilder()
            .EnableConversations()
            .AddStart()
            .AddSuspend("ask")
            .AddEnd()
            .Connect("start", "ask")
            .Connect("ask", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = NewConversationId();

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var suspendedRun = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Suspended, suspendedRun.RunStatus);

            var failedRun = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId, new UnknownResumeInput());
            Assert.Equal(RunStatus.Failed, failedRun.RunStatus);
            Assert.Equal("Suspend node cannot handle resume input type 'UnknownResumeInput'.", failedRun.Error);

            var conversation = await repositoryService.GetConversation(conversationId);
            Assert.NotNull(conversation);
            Assert.Equal(ConversationStatus.Suspended, conversation!.Status);
            Assert.Equal(failedRun.Error, conversation.LastError);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static ContextMergeResumeInput CreateContextResumeInput(string path, object? value)
    {
        ContextObject context = [];
        if (!context.TrySet(path, value))
            throw new InvalidOperationException($"Could not set '{path}' into test resume context.");

        return new ContextMergeResumeInput() { Context = context };
    }

    private static ContextMergeResumeInput CreateContextResumeInput(params (string Path, object? Value)[] values)
    {
        ContextObject context = [];
        foreach (var (path, value) in values)
            if (!context.TrySet(path, value))
                throw new InvalidOperationException($"Could not set '{path}' into test resume context.");

        return new ContextMergeResumeInput() { Context = context };
    }

    private static AgUiAgentResumeInput CreateAgUiAgentResumeInput(params (string Path, object? Value)[] values)
    {
        var agentJson = new System.Text.Json.Nodes.JsonObject();
        foreach (var (path, value) in values)
        {
            SetJsonPath(agentJson, path, value);
        }

        var agent = ContextHelpers.FastDeserializeString(agentJson.ToJsonString()) as ContextObject
            ?? throw new InvalidOperationException("Could not convert test AG-UI payload into a ContextObject.");

        return new AgUiAgentResumeInput() { Agent = agent };
    }

    private static void SetJsonPath(System.Text.Json.Nodes.JsonObject root, string path, object? value)
    {
        var segments = ParsePath(path);
        System.Text.Json.Nodes.JsonNode current = root;

        for (var i = 0; i < segments.Count - 1; i++)
        {
            var segment = segments[i];
            var next = segments[i + 1];

            if (segment.PropertyName is not null)
            {
                var obj = current as System.Text.Json.Nodes.JsonObject
                    ?? throw new InvalidOperationException($"Path '{path}' expected an object segment.");

                if (!obj.TryGetPropertyValue(segment.PropertyName, out var child) || child is null)
                {
                    child = next.PropertyName is null
                        ? new System.Text.Json.Nodes.JsonArray()
                        : new System.Text.Json.Nodes.JsonObject();
                    obj[segment.PropertyName] = child;
                }

                current = child;
                continue;
            }

            var array = current as System.Text.Json.Nodes.JsonArray
                ?? throw new InvalidOperationException($"Path '{path}' expected an array segment.");

            while (array.Count <= segment.Index)
                array.Add(null);

            var arrayChild = array[segment.Index];
            if (arrayChild is null)
            {
                arrayChild = next.PropertyName is null
                    ? new System.Text.Json.Nodes.JsonArray()
                    : new System.Text.Json.Nodes.JsonObject();
                array[segment.Index] = arrayChild;
            }

            current = arrayChild;
        }

        var last = segments[^1];
        var valueNode = System.Text.Json.JsonSerializer.SerializeToNode(value);
        if (last.PropertyName is not null)
        {
            var obj = current as System.Text.Json.Nodes.JsonObject
                ?? throw new InvalidOperationException($"Path '{path}' expected an object value target.");
            obj[last.PropertyName] = valueNode;
            return;
        }

        var targetArray = current as System.Text.Json.Nodes.JsonArray
            ?? throw new InvalidOperationException($"Path '{path}' expected an array value target.");

        while (targetArray.Count <= last.Index)
            targetArray.Add(null);

        targetArray[last.Index] = valueNode;
    }

    private static List<(string? PropertyName, int Index)> ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("AG-UI test path cannot be empty.");

        List<(string? PropertyName, int Index)> segments = [];
        var buffer = new System.Text.StringBuilder();

        for (var i = 0; i < path.Length; i++)
        {
            var c = path[i];
            if (c == '.')
            {
                if (buffer.Length == 0)
                {
                    if (i > 0 && path[i - 1] == ']')
                        continue;

                    throw new InvalidOperationException($"AG-UI test path '{path}' is invalid.");
                }

                segments.Add((buffer.ToString(), -1));
                buffer.Clear();
                continue;
            }

            if (c == '[')
            {
                if (buffer.Length > 0)
                {
                    segments.Add((buffer.ToString(), -1));
                    buffer.Clear();
                }

                var endBracket = path.IndexOf(']', i + 1);
                if (endBracket < 0)
                    throw new InvalidOperationException($"AG-UI test path '{path}' is invalid.");

                var indexText = path[(i + 1)..endBracket];
                if (!int.TryParse(indexText, out var index))
                    throw new InvalidOperationException($"AG-UI test path '{path}' has an invalid index.");

                segments.Add((null, index));
                i = endBracket;
                continue;
            }

            buffer.Append(c);
        }

        if (buffer.Length > 0)
            segments.Add((buffer.ToString(), -1));

        if (segments.Count == 0)
            throw new InvalidOperationException($"AG-UI test path '{path}' is invalid.");

        return segments;
    }

    private static string NewConversationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static async Task<Run> WaitForConversationRun(IRepositoryService repositoryService, string conversationId, RunStatus expectedStatus)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var runs = await repositoryService.GetConversationRuns(conversationId);
            var run = runs.OrderByDescending(r => r.Created).FirstOrDefault();
            if (run is not null && run.RunStatus == expectedStatus)
                return run;

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException($"Conversation '{conversationId}' did not reach run status '{expectedStatus}'.");
    }

    private sealed class UnknownResumeInput : NodeResumeInput { }
}

