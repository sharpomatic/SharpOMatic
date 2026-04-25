namespace SharpOMatic.Tests.AgUi;

public sealed class AgUiChatHistoryIntegrationTests
{
    [Fact]
    public async Task AgUi_non_conversation_workflow_passes_full_message_history_to_model_call()
    {
        var model = CreateModel("openai");
        var workflow = CreateModelWorkflow(model.ModelId, isConversationEnabled: false, prompt: string.Empty);
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
        var workflow = CreateModelWorkflow(model.ModelId, isConversationEnabled: true, prompt: "{{$agent.latestUserMessage.content}}");
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
            Assert.Null(firstTurnChat[0].MessageId);
            Assert.Equal("First prompt", Assert.IsType<TextContent>(firstTurnChat[0].Contents.Single()).Text);

            var secondTurnChat = capture.CapturedChats[1];
            Assert.Equal(3, secondTurnChat.Count);
            Assert.Null(secondTurnChat[0].MessageId);
            Assert.Equal("First prompt", Assert.IsType<TextContent>(secondTurnChat[0].Contents.Single()).Text);
            Assert.Equal("assistant-1", secondTurnChat[1].MessageId);
            Assert.Equal(ChatRole.Assistant, secondTurnChat[1].Role);
            Assert.Null(secondTurnChat[2].MessageId);
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

    [Fact]
    public async Task AgUi_conversation_workflow_persists_latest_user_message_as_silent_stream_history()
    {
        var workflow = new SharpOMatic.Tests.Workflows.WorkflowBuilder()
            .WithName("User History Workflow")
            .EnableConversations()
            .AddStart()
            .AddEnd()
            .Connect("start", "end")
            .Build();

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IAgUiRunEventBroker, AgUiRunEventBroker>();
            services.AddSingleton<IProgressService, AgUiProgressService>();
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
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
                    Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello history" }]"""),
                    ForwardedProps = ParseJson($$"""{"workflowId":"{{workflow.Id}}"}"""),
                }
            );

            var liveEvents = ReadSseEvents((MemoryStream)controller.HttpContext.Response.Body);
            Assert.DoesNotContain(liveEvents, e =>
                e.GetProperty("type").GetString() == "TEXT_MESSAGE_START"
                && e.TryGetProperty("role", out var role)
                && role.GetString() == "user"
            );

            var run = Assert.Single(await repositoryService.GetConversationRuns("thread-1"));
            var storedEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Collection(
                storedEvents,
                streamEvent =>
                {
                    Assert.Equal(StreamEventKind.TextStart, streamEvent.EventKind);
                    Assert.Equal(StreamMessageRole.User, streamEvent.MessageRole);
                    Assert.Equal("user-1", streamEvent.MessageId);
                    Assert.Equal(1, streamEvent.SequenceNumber);
                },
                streamEvent =>
                {
                    Assert.Equal(StreamEventKind.TextContent, streamEvent.EventKind);
                    Assert.Equal("user-1", streamEvent.MessageId);
                    Assert.Equal("Hello history", streamEvent.TextDelta);
                    Assert.Equal(2, streamEvent.SequenceNumber);
                },
                streamEvent =>
                {
                    Assert.Equal(StreamEventKind.TextEnd, streamEvent.EventKind);
                    Assert.Equal("user-1", streamEvent.MessageId);
                    Assert.Equal(3, streamEvent.SequenceNumber);
                }
            );
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task AgUi_backend_tool_call_workflow_streams_complete_tool_call_lifecycle()
    {
        var workflow = new SharpOMatic.Tests.Workflows.WorkflowBuilder()
            .WithName("Backend Tool Call Workflow")
            .AddStart()
            .AddBackendToolCall(
                functionName: "lookup_weather",
                argumentsMode: ToolCallDataMode.FixedJson,
                argumentsJson: """{"city":"Sydney"}""",
                resultMode: ToolCallDataMode.FixedJson,
                resultJson: """{"forecast":"Sunny"}"""
            )
            .AddEnd()
            .Connect("start", "BE Tool Call")
            .Connect("BE Tool Call", "end")
            .Build();

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IAgUiRunEventBroker, AgUiRunEventBroker>();
            services.AddSingleton<IProgressService, AgUiProgressService>();
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
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
                    ForwardedProps = ParseJson($$"""{"workflowId":"{{workflow.Id}}"}"""),
                }
            );

            var events = ReadSseEvents((MemoryStream)controller.HttpContext.Response.Body);
            Assert.Equal("RUN_STARTED", events[0].GetProperty("type").GetString());

            var toolStart = Assert.Single(events, e => e.GetProperty("type").GetString() == "TOOL_CALL_START");
            var toolArgs = Assert.Single(events, e => e.GetProperty("type").GetString() == "TOOL_CALL_ARGS");
            var toolEnd = Assert.Single(events, e => e.GetProperty("type").GetString() == "TOOL_CALL_END");
            var toolResult = Assert.Single(events, e => e.GetProperty("type").GetString() == "TOOL_CALL_RESULT");

            var toolCallId = toolStart.GetProperty("toolCallId").GetString();
            Assert.Equal("lookup_weather", toolStart.GetProperty("toolCallName").GetString());
            Assert.Equal(toolCallId, toolArgs.GetProperty("toolCallId").GetString());
            Assert.Equal("{\"city\":\"Sydney\"}", toolArgs.GetProperty("delta").GetString());
            Assert.Equal(toolCallId, toolEnd.GetProperty("toolCallId").GetString());
            Assert.Equal(toolCallId, toolResult.GetProperty("toolCallId").GetString());
            Assert.Equal("tool", toolResult.GetProperty("role").GetString());
            Assert.Equal("{\"forecast\":\"Sunny\"}", toolResult.GetProperty("content").GetString());

            var protocolToolMessageId = toolResult.GetProperty("messageId").GetString();
            Assert.NotNull(protocolToolMessageId);
            Assert.StartsWith("tool:backend-tool-result:", protocolToolMessageId);
            Assert.Equal("RUN_FINISHED", events[^1].GetProperty("type").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task AgUi_step_nodes_workflow_streams_step_lifecycle()
    {
        var workflow = new SharpOMatic.Tests.Workflows.WorkflowBuilder()
            .WithName("Step Workflow")
            .AddStart()
            .AddStepStart(stepName: "Search")
            .AddStepEnd(stepName: "Search")
            .AddEnd()
            .Connect("start", "Step Start")
            .Connect("Step Start", "Step End")
            .Connect("Step End", "end")
            .Build();

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IAgUiRunEventBroker, AgUiRunEventBroker>();
            services.AddSingleton<IProgressService, AgUiProgressService>();
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
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
                    RunId = "protocol-run-step-1",
                    ForwardedProps = ParseJson($$"""{"workflowId":"{{workflow.Id}}"}"""),
                }
            );

            var events = ReadSseEvents((MemoryStream)controller.HttpContext.Response.Body);
            Assert.Equal("RUN_STARTED", events[0].GetProperty("type").GetString());

            var stepStart = Assert.Single(events, e => e.GetProperty("type").GetString() == "STEP_STARTED");
            var stepEnd = Assert.Single(events, e => e.GetProperty("type").GetString() == "STEP_FINISHED");
            Assert.Equal("Search", stepStart.GetProperty("stepName").GetString());
            Assert.Equal("Search", stepEnd.GetProperty("stepName").GetString());
            Assert.Equal("RUN_FINISHED", events[^1].GetProperty("type").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task AgUi_state_sync_node_workflow_streams_state_snapshot_and_delta()
    {
        var requestStateJson = JsonSerializer.Serialize(new { title = new string('x', 200), mode = "assistant" });
        var workflow = new SharpOMatic.Tests.Workflows.WorkflowBuilder()
            .WithName("State Sync Workflow")
            .EnableConversations()
            .AddStart()
            .AddCode(
                "update",
                """
                var turn = Context.TryGet<int>("state.turn", out var currentTurn) ? currentTurn + 1 : 1;
                Context.Set("state.turn", turn);
                Context.Set("agent.state.title", new string('x', 200));

                if (turn == 1)
                    Context.Set("agent.state.mode", "assistant");
                else
                    Context.Set("agent.state.mode", "review");
                """
            )
            .AddStateSync()
            .AddEnd()
            .Connect("start", "update")
            .Connect("update", "State Sync")
            .Connect("State Sync", "end")
            .Build();

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider(services =>
        {
            services.AddSingleton<IAgUiRunEventBroker, AgUiRunEventBroker>();
            services.AddSingleton<IProgressService, AgUiProgressService>();
        });

        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            var controller = new AgUiController(provider.GetRequiredService<IEngineService>(), provider.GetRequiredService<IAgUiRunEventBroker>());

            var firstHttpContext = CreateHttpContext(provider);
            controller.ControllerContext = new ControllerContext() { HttpContext = firstHttpContext };
            await controller.Post(
                new AgUiRunRequest()
                {
                    ThreadId = "thread-1",
                    RunId = "protocol-run-state-1",
                    ForwardedProps = ParseJson($$"""{"workflowId":"{{workflow.Id}}"}"""),
                }
            );
            await WaitForConversationToBeIdleAsync(repositoryService, "thread-1");

            var secondHttpContext = CreateHttpContext(provider);
            controller.ControllerContext = new ControllerContext() { HttpContext = secondHttpContext };
            await controller.Post(
                new AgUiRunRequest()
                {
                    ThreadId = "thread-1",
                    RunId = "protocol-run-state-2",
                    State = ParseJson(requestStateJson),
                    ForwardedProps = ParseJson($$"""{"workflowId":"{{workflow.Id}}"}"""),
                }
            );

            var firstEvents = ReadSseEvents((MemoryStream)firstHttpContext.Response.Body);
            var secondEvents = ReadSseEvents((MemoryStream)secondHttpContext.Response.Body);
            Assert.Equal("RUN_STARTED", firstEvents[0].GetProperty("type").GetString());
            Assert.Equal("RUN_STARTED", secondEvents[0].GetProperty("type").GetString());

            var stateSnapshot = Assert.Single(firstEvents, e => e.GetProperty("type").GetString() == "STATE_SNAPSHOT");
            var stateDelta = Assert.Single(secondEvents, e => e.GetProperty("type").GetString() == "STATE_DELTA");
            Assert.Equal("assistant", stateSnapshot.GetProperty("snapshot").GetProperty("mode").GetString());
            Assert.Equal("replace", stateDelta.GetProperty("delta")[0].GetProperty("op").GetString());
            Assert.Equal("/mode", stateDelta.GetProperty("delta")[0].GetProperty("path").GetString());
            Assert.Equal("review", stateDelta.GetProperty("delta")[0].GetProperty("value").GetString());
            Assert.Equal("RUN_FINISHED", firstEvents[^1].GetProperty("type").GetString());
            Assert.Equal("RUN_FINISHED", secondEvents[^1].GetProperty("type").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static WorkflowEntity CreateModelWorkflow(Guid modelId, bool isConversationEnabled, string prompt)
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
        node.Prompt = prompt;
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
            AddChatInputPathMessages(chat, threadContext, node);
            await ResolveInstructionsAndPrompt(chat, processContext, threadContext, node);
            capture.CapturedChats.Add(CloneMessages(chat));

            var turnNumber = processContext.Run.TurnNumber ?? 1;
            IList<ChatMessage> responses =
            [
                new ChatMessage(ChatRole.Assistant, [new TextContent($"reply-{turnNumber}")]) { MessageId = $"assistant-{turnNumber}" },
            ];

            return (chat, responses, $"reply-{turnNumber}");
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
