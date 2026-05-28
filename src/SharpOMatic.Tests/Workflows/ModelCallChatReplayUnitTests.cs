
namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallChatReplayUnitTests
{
    [Fact]
    public async Task Conversation_chat_replay_always_strips_reasoning_even_when_model_id_is_unchanged()
    {
        var workflowId = Guid.NewGuid();
        var model = CreateModel("openai");
        var workflow = CreateReplayWorkflow(workflowId, model.ModelId);
        var capture = new ChatReplayCapture();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton(capture);
            services.AddKeyedScoped<IModelCaller, ReplayAwareTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = Guid.NewGuid().ToString("N");

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Success, firstTurn.RunStatus);
            await Task.Delay(100);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            Assert.Equal(2, capture.CapturedChats.Count);
            Assert.Empty(capture.CapturedChats[0]);

            var replayedChat = capture.CapturedChats[1];
            Assert.Equal(3, replayedChat.Count);

            Assert.Equal(ChatRole.Assistant, replayedChat[0].Role);
            Assert.Equal("Portable text", Assert.IsType<TextContent>(replayedChat[0].Contents.Single()).Text);

            Assert.Equal(ChatRole.Assistant, replayedChat[1].Role);
            Assert.Equal(
                "Result of calling tool lookup_weather with arguments {\"city\":\"Sydney\"} = Sunny",
                Assert.IsType<TextContent>(replayedChat[1].Contents.Single()).Text
            );

            Assert.Equal(ChatRole.Assistant, replayedChat[2].Role);
            Assert.Equal(
                "Result of calling tool get_time with no arguments = Noon",
                Assert.IsType<TextContent>(replayedChat[2].Contents.Single()).Text
            );

            Assert.All(replayedChat, message =>
            {
                Assert.True(message.Role == ChatRole.User || message.Role == ChatRole.Assistant);
                Assert.DoesNotContain(message.Contents, content => content is TextReasoningContent or FunctionCallContent or FunctionResultContent);
            });
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_chat_replay_strips_reasoning_when_model_id_changes()
    {
        var workflowId = Guid.NewGuid();
        var firstModel = CreateModel("openai");
        var secondModel = CreateModel("openai");
        var workflow = CreateReplayWorkflow(workflowId, firstModel.ModelId);
        var capture = new ChatReplayCapture();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton(capture);
            services.AddKeyedScoped<IModelCaller, ReplayAwareTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", firstModel);
        await SeedModelCallMetadata(repositoryService, "openai", secondModel);
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = Guid.NewGuid().ToString("N");

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Success, firstTurn.RunStatus);
            await Task.Delay(100);

            var updatedWorkflow = CreateReplayWorkflow(workflowId, secondModel.ModelId);
            await repositoryService.UpsertWorkflow(updatedWorkflow);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflowId, conversationId);
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            Assert.Equal(2, capture.CapturedChats.Count);
            var replayedChat = capture.CapturedChats[1];
            Assert.Equal(3, replayedChat.Count);

            var sanitizedMessage = replayedChat[0];
            Assert.Equal(ChatRole.Assistant, sanitizedMessage.Role);
            Assert.Equal("Portable text", Assert.IsType<TextContent>(sanitizedMessage.Contents.Single()).Text);
            Assert.Equal(ChatRole.Assistant, replayedChat[1].Role);
            Assert.Equal("Result of calling tool lookup_weather with arguments {\"city\":\"Sydney\"} = Sunny", Assert.IsType<TextContent>(replayedChat[1].Contents.Single()).Text);
            Assert.Equal(ChatRole.Assistant, replayedChat[2].Role);
            Assert.Equal("Result of calling tool get_time with no arguments = Noon", Assert.IsType<TextContent>(replayedChat[2].Contents.Single()).Text);
            Assert.All(replayedChat, message =>
            {
                Assert.True(message.Role == ChatRole.User || message.Role == ChatRole.Assistant);
                Assert.DoesNotContain(message.Contents, content => content is TextReasoningContent or FunctionCallContent or FunctionResultContent);
            });
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Conversation_chat_replay_can_drop_tool_calls_from_chat_output()
    {
        var workflowId = Guid.NewGuid();
        var model = CreateModel("openai");
        var workflow = CreateReplayWorkflow(workflowId, model.ModelId, dropToolCalls: true);
        var capture = new ChatReplayCapture();

        using var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton(capture);
            services.AddKeyedScoped<IModelCaller, ReplayAwareTestModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        var conversationId = Guid.NewGuid().ToString("N");

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();

            var firstTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Success, firstTurn.RunStatus);
            await Task.Delay(100);

            var secondTurn = await engineService.StartOrResumeConversationAndWait(workflow.Id, conversationId);
            Assert.Equal(RunStatus.Success, secondTurn.RunStatus);

            Assert.Equal(2, capture.CapturedChats.Count);
            var replayedChat = capture.CapturedChats[1];
            var sanitizedMessage = Assert.Single(replayedChat);
            Assert.Equal(ChatRole.Assistant, sanitizedMessage.Role);
            Assert.Equal("Portable text", Assert.IsType<TextContent>(sanitizedMessage.Contents.Single()).Text);
            Assert.DoesNotContain(sanitizedMessage.Contents, content => content is TextReasoningContent or FunctionCallContent or FunctionResultContent);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static WorkflowEntity CreateReplayWorkflow(Guid workflowId, Guid modelId, bool dropToolCalls = false)
    {
        var workflow = new WorkflowBuilder()
            .WithId(workflowId)
            .EnableConversations()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(n => n.Title == "model"));
        node.ModelId = modelId;
        node.Prompt = string.Empty;
        node.DropToolCalls = dropToolCalls;
        node.ChatInputPath = "conversation.chat";
        node.ChatOutputPath = "conversation.chat";
        node.TextOutputPath = "output.text";

        return workflow;
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

    private sealed class ReplayAwareTestModelCaller(ChatReplayCapture capture) : BaseModelCaller
    {
        public override Task<ModelCallResult> Call(
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
            AddChatInputPathMessages(chat, threadContext, node);
            capture.CapturedChats.Add(CloneMessages(chat));

            var turnNumber = processContext.Run.TurnNumber ?? 1;
            IList<ChatMessage> responses = turnNumber == 1 ? BuildSeedResponses() : [new ChatMessage(ChatRole.Assistant, [new TextContent($"turn-{turnNumber}")])];

            return Task.FromResult<ModelCallResult>((chat, responses, $"turn-{turnNumber}"));
        }

        private static List<ChatMessage> CloneMessages(IEnumerable<ChatMessage> messages)
        {
            List<ChatMessage> clone = [];
            foreach (var message in messages)
            {
                List<AIContent> contents = [];
                foreach (var content in message.Contents)
                {
                    switch (content)
                    {
                        case TextContent textContent:
                            contents.Add(new TextContent(textContent.Text));
                            break;
                        case TextReasoningContent reasoningContent:
                            contents.Add(new TextReasoningContent(reasoningContent.Text) { ProtectedData = reasoningContent.ProtectedData });
                            break;
                        case FunctionCallContent functionCallContent:
                            contents.Add(
                                new FunctionCallContent(
                                    functionCallContent.CallId,
                                    functionCallContent.Name,
                                    functionCallContent.Arguments is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(functionCallContent.Arguments)
                                )
                            );
                            break;
                        case FunctionResultContent functionResultContent:
                            contents.Add(new FunctionResultContent(functionResultContent.CallId, functionResultContent.Result));
                            break;
                    }
                }

                clone.Add(
                    new ChatMessage(message.Role, contents)
                    {
                        AuthorName = message.AuthorName,
                        CreatedAt = message.CreatedAt,
                        MessageId = message.MessageId,
                    }
                );
            }

            return clone;
        }

        private static IList<ChatMessage> BuildSeedResponses()
        {
            return
            [
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new TextContent("Portable text"),
                        new TextReasoningContent("Hidden reasoning") { ProtectedData = "reasoning-secret" },
                        new FunctionCallContent("call-1", "lookup_weather", new Dictionary<string, object?>() { ["city"] = "Sydney" }),
                        new FunctionResultContent("call-1", "Sunny"),
                        new FunctionCallContent("call-2", "get_time", new Dictionary<string, object?>()),
                        new FunctionResultContent("call-2", "Noon"),
                    ]
                ),
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new TextReasoningContent("Reasoning only"),
                    ]
                ),
            ];
        }
    }

    private sealed class ChatReplayCapture
    {
        public List<List<ChatMessage>> CapturedChats { get; } = [];
    }
}
