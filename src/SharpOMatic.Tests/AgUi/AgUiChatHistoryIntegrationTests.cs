namespace SharpOMatic.Tests.AgUi;

public sealed class AgUiChatHistoryIntegrationTests
{
    [Fact]
    public async Task AgUi_non_conversation_workflow_passes_full_message_history_to_model_call()
    {
        var model = CreateModel("openai");
        var workflow = CreateModelWorkflow(model.ModelId, isConversationEnabled: false);
        var capture = new AgUiChatCapture();

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IAgUiRunEventBroker, AgUiRunEventBroker>();
            services.AddSingleton<IProgressService, AgUiProgressService>();
            services.AddSingleton(capture);
            services.AddKeyedScoped<IModelCaller, AgUiCaptureModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            var controller = new AgUiController(provider.GetRequiredService<IEngineService>(), provider.GetRequiredService<IAgUiRunEventBroker>());
            controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

            var request = new AgUiRunRequest()
            {
                ThreadId = "thread-1",
                RunId = "protocol-run-1",
                Messages = ParseJson(
                    """
                    [
                      { "id": "system-1", "role": "system", "content": "System prompt" },
                      { "id": "developer-1", "role": "developer", "content": "Developer prompt" },
                      { "id": "user-1", "role": "user", "content": "Hello" },
                      {
                        "id": "assistant-1",
                        "role": "assistant",
                        "content": "Calling tool",
                        "toolCalls": [
                          {
                            "id": "call-1",
                            "type": "function",
                            "function": {
                              "name": "lookup_weather",
                              "arguments": "{\"city\":\"Sydney\"}"
                            }
                          }
                        ]
                      },
                      { "id": "tool-1", "role": "tool", "toolCallId": "call-1", "content": "Sunny" }
                    ]
                    """
                ),
                ForwardedProps = ParseJson($$"""{"workflowId":"{{workflow.Id}}"}"""),
            };

            await controller.Post(request);

            var capturedChat = Assert.Single(capture.CapturedChats);
            Assert.Equal(5, capturedChat.Count);
            Assert.Equal("system-1", capturedChat[0].MessageId);
            Assert.Equal(ChatRole.System, capturedChat[0].Role);
            Assert.Equal("developer-1", capturedChat[1].MessageId);
            Assert.Equal(ChatRole.System, capturedChat[1].Role);
            Assert.Equal("user-1", capturedChat[2].MessageId);
            Assert.Equal(ChatRole.User, capturedChat[2].Role);

            var assistantMessage = capturedChat[3];
            Assert.Equal("assistant-1", assistantMessage.MessageId);
            Assert.Equal(ChatRole.Assistant, assistantMessage.Role);
            Assert.Equal(2, assistantMessage.Contents.Count);
            Assert.Equal("Calling tool", Assert.IsType<TextContent>(assistantMessage.Contents[0]).Text);

            var functionCall = Assert.IsType<FunctionCallContent>(assistantMessage.Contents[1]);
            Assert.Equal("call-1", functionCall.CallId);
            Assert.Equal("lookup_weather", functionCall.Name);
            Assert.NotNull(functionCall.Arguments);
            Assert.Equal("Sydney", functionCall.Arguments["city"]?.ToString());

            var toolMessage = capturedChat[4];
            Assert.Equal("tool-1", toolMessage.MessageId);
            Assert.Equal(ChatRole.Tool, toolMessage.Role);
            Assert.Equal("Sunny", Assert.IsType<FunctionResultContent>(toolMessage.Contents.Single()).Result?.ToString());

            var events = ReadSseEvents((MemoryStream)controller.HttpContext.Response.Body);
            Assert.Equal("RUN_STARTED", events[0].GetProperty("type").GetString());
            Assert.Equal("RUN_FINISHED", events[^1].GetProperty("type").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task AgUi_conversation_workflow_replays_stored_input_chat_and_appends_incremental_messages()
    {
        var model = CreateModel("openai");
        var workflow = CreateModelWorkflow(model.ModelId, isConversationEnabled: true);
        var capture = new AgUiChatCapture();

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IAgUiRunEventBroker, AgUiRunEventBroker>();
            services.AddSingleton<IProgressService, AgUiProgressService>();
            services.AddSingleton(capture);
            services.AddKeyedScoped<IModelCaller, AgUiCaptureModelCaller>("openai");
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repositoryService, "openai", model);
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            var controller = new AgUiController(provider.GetRequiredService<IEngineService>(), provider.GetRequiredService<IAgUiRunEventBroker>());

            controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };
            await controller.Post(
                new AgUiRunRequest()
                {
                    ThreadId = "thread-1",
                    RunId = "protocol-run-1",
                    Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "First prompt" }]"""),
                    ForwardedProps = ParseJson($$"""{"workflowId":"{{workflow.Id}}"}"""),
                }
            );
            await WaitForConversationToBeIdleAsync(repositoryService, "thread-1");

            controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };
            await controller.Post(
                new AgUiRunRequest()
                {
                    ThreadId = "thread-1",
                    RunId = "protocol-run-2",
                    Messages = ParseJson("""[{ "id": "user-2", "role": "user", "content": "Second prompt" }]"""),
                    ForwardedProps = ParseJson($$"""{"workflowId":"{{workflow.Id}}"}"""),
                }
            );

            Assert.Equal(2, capture.CapturedChats.Count);

            var firstTurnChat = capture.CapturedChats[0];
            Assert.Single(firstTurnChat);
            Assert.Equal("user-1", firstTurnChat[0].MessageId);

            var secondTurnChat = capture.CapturedChats[1];
            Assert.Equal(3, secondTurnChat.Count);
            Assert.Equal("user-1", secondTurnChat[0].MessageId);
            Assert.Equal("assistant-1", secondTurnChat[1].MessageId);
            Assert.Equal(ChatRole.Assistant, secondTurnChat[1].Role);
            Assert.Equal("user-2", secondTurnChat[2].MessageId);
            Assert.Equal("Second prompt", Assert.IsType<TextContent>(secondTurnChat[2].Contents.Single()).Text);

            var events = ReadSseEvents((MemoryStream)controller.HttpContext.Response.Body);
            Assert.Equal("RUN_STARTED", events[0].GetProperty("type").GetString());
            Assert.Equal("RUN_FINISHED", events[^1].GetProperty("type").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static WorkflowEntity CreateModelWorkflow(Guid modelId, bool isConversationEnabled)
    {
        var workflow = new SharpOMatic.Tests.Workflows.WorkflowBuilder()
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end");

        if (isConversationEnabled)
            workflow.EnableConversations();

        var definition = workflow.Build();
        var node = Assert.IsType<ModelCallNodeEntity>(definition.Nodes.Single(n => n.Title == "model"));
        node.ModelId = modelId;
        node.Prompt = string.Empty;
        node.ChatInputPath = "input.chat";
        node.ChatOutputPath = isConversationEnabled ? "input.chat" : string.Empty;
        node.TextOutputPath = "output.text";
        return definition;
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

    private static DefaultHttpContext CreateHttpContext(IServiceProvider requestServices)
    {
        return new DefaultHttpContext()
        {
            RequestServices = requestServices,
            Response =
            {
                Body = new MemoryStream(),
            }
        };
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static List<JsonElement> ReadSseEvents(MemoryStream responseBody)
    {
        responseBody.Position = 0;

            return Encoding.UTF8
            .GetString(responseBody.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("data: ", StringComparison.Ordinal))
            .Select(line =>
            {
                using var document = JsonDocument.Parse(line["data: ".Length..]);
                return document.RootElement.Clone();
            })
            .ToList();
    }

    private static async Task WaitForConversationToBeIdleAsync(IRepositoryService repositoryService, string conversationId)
    {
        for (var attempt = 0; attempt < 20; attempt += 1)
        {
            var conversation = await repositoryService.GetConversation(conversationId);
            if (conversation is null || string.IsNullOrWhiteSpace(conversation.LeaseOwner))
                return;

            await Task.Delay(25);
        }

        throw new Xunit.Sdk.XunitException($"Conversation '{conversationId}' did not release its lease in time.");
    }

    private sealed class AgUiCaptureModelCaller(AgUiChatCapture capture) : BaseModelCaller
    {
        public override Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)> Call(
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
            IList<ChatMessage> responses =
            [
                new ChatMessage(ChatRole.Assistant, [new TextContent($"reply-{turnNumber}")]) { MessageId = $"assistant-{turnNumber}" },
            ];

            return Task.FromResult<(IList<ChatMessage> chat, IList<ChatMessage> responses, object? resultValue)>((chat, responses, $"reply-{turnNumber}"));
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
    }

    private sealed class AgUiChatCapture
    {
        public List<List<ChatMessage>> CapturedChats { get; } = [];
    }
}
