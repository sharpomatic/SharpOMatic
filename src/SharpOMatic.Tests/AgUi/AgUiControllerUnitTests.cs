
namespace SharpOMatic.Tests.AgUi;

public sealed class AgUiControllerUnitTests
{
    [Fact]
    public async Task AgUi_controller_emits_run_started_and_run_finished_events_with_expected_payload()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        var events = ReadSseEvents((MemoryStream)httpContext.Response.Body);

        Assert.Collection(
            events,
            startedEvent =>
            {
                Assert.Equal("RUN_STARTED", startedEvent.GetProperty("type").GetString());
                Assert.Equal("thread-1", startedEvent.GetProperty("threadId").GetString());
                Assert.Equal("protocol-run-1", startedEvent.GetProperty("runId").GetString());
            },
            finishedEvent =>
            {
                Assert.Equal("RUN_FINISHED", finishedEvent.GetProperty("type").GetString());
                Assert.Equal("thread-1", finishedEvent.GetProperty("threadId").GetString());
                Assert.Equal("protocol-run-1", finishedEvent.GetProperty("runId").GetString());
            }
        );
    }

    [Fact]
    public async Task AgUi_controller_emits_run_started_and_run_error_events_with_expected_payload_for_failed_run()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Failed,
                    Error = "Boom",
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        var events = ReadSseEvents((MemoryStream)httpContext.Response.Body);

        Assert.Collection(
            events,
            startedEvent =>
            {
                Assert.Equal("RUN_STARTED", startedEvent.GetProperty("type").GetString());
                Assert.Equal("thread-1", startedEvent.GetProperty("threadId").GetString());
                Assert.Equal("protocol-run-1", startedEvent.GetProperty("runId").GetString());
            },
            errorEvent =>
            {
                Assert.Equal("RUN_ERROR", errorEvent.GetProperty("type").GetString());
                Assert.Equal("thread-1", errorEvent.GetProperty("threadId").GetString());
                Assert.Equal("protocol-run-1", errorEvent.GetProperty("runId").GetString());
                Assert.Equal("Boom", errorEvent.GetProperty("message").GetString());
            }
        );
    }

    [Fact]
    public async Task AgUi_controller_uses_agui_agent_resume_input()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        NodeResumeInput? capturedResumeInput = null;

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .Callback<Guid, string, NodeResumeInput?, ContextEntryListEntity?, bool, string?>((_, _, resumeInput, _, _, _) => capturedResumeInput = resumeInput)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            State = ParseJson("""{ "mode": "assistant" }"""),
            Context = ParseJson("""[{ "id": "ctx-1" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        var resumeInput = Assert.IsType<AgUiAgentResumeInput>(capturedResumeInput);
        Assert.Equal("Hello", resumeInput.Agent.Get<string>("latestUserMessage.content"));
        Assert.Equal("Hello", resumeInput.Agent.Get<string>("messages[0].content"));
        Assert.False(resumeInput.Agent.TryGet<ContextObject>("latestToolResult", out _));
        Assert.Equal("assistant", resumeInput.Agent.Get<string>("state.mode"));
        Assert.Equal("ctx-1", resumeInput.Agent.Get<string>("context[0].id"));
    }

    [Fact]
    public async Task AgUi_controller_only_sets_latest_user_message_when_latest_message_is_user_text()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        NodeResumeInput? capturedResumeInput = null;

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .Callback<Guid, string, NodeResumeInput?, ContextEntryListEntity?, bool, string?>((_, _, resumeInput, _, _, _) => capturedResumeInput = resumeInput)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            Messages = ParseJson("""
                [
                  { "id": "user-1", "role": "user", "content": "Hello" },
                  { "id": "assistant-1", "role": "assistant", "content": "Hi" }
                ]
                """),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        var resumeInput = Assert.IsType<AgUiAgentResumeInput>(capturedResumeInput);
        var messages = resumeInput.Agent.Get<ContextList>("messages");
        Assert.Single(messages);
        Assert.Equal("Hi", resumeInput.Agent.Get<string>("messages[0].content"));
        Assert.False(resumeInput.Agent.TryGet<ContextObject>("latestUserMessage", out _));
        Assert.False(resumeInput.Agent.TryGet<ContextObject>("latestToolResult", out _));
    }

    [Fact]
    public async Task AgUi_controller_sets_latest_tool_result_only_when_latest_message_is_tool_result()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        NodeResumeInput? capturedResumeInput = null;

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .Callback<Guid, string, NodeResumeInput?, ContextEntryListEntity?, bool, string?>((_, _, resumeInput, _, _, _) => capturedResumeInput = resumeInput)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            Messages = ParseJson("""
                [
                  { "id": "user-1", "role": "user", "content": "Hello" },
                  { "id": "tool-1", "role": "tool", "toolCallId": "call-1", "content": "{\"status\":\"Sunny\",\"temperatureC\":24}" }
                ]
                """),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        var resumeInput = Assert.IsType<AgUiAgentResumeInput>(capturedResumeInput);
        Assert.Equal("{\"status\":\"Sunny\",\"temperatureC\":24}", resumeInput.Agent.Get<string>("latestToolResult.content"));
        Assert.Equal("Sunny", resumeInput.Agent.Get<string>("latestToolResult.value.status"));
        Assert.Equal(24, resumeInput.Agent.Get<int>("latestToolResult.value.temperatureC"));
        Assert.Equal("call-1", resumeInput.Agent.Get<string>("latestToolResult.toolCallId"));
        var messages = resumeInput.Agent.Get<ContextList>("messages");
        Assert.Single(messages);
        Assert.Equal("{\"status\":\"Sunny\",\"temperatureC\":24}", resumeInput.Agent.Get<string>("messages[0].content"));
        Assert.False(resumeInput.Agent.TryGet<ContextObject>("latestUserMessage", out _));
    }

    [Fact]
    public async Task AgUi_controller_keeps_empty_latest_tool_result_content_as_string()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        NodeResumeInput? capturedResumeInput = null;

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .Callback<Guid, string, NodeResumeInput?, ContextEntryListEntity?, bool, string?>((_, _, resumeInput, _, _, _) => capturedResumeInput = resumeInput)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            Messages = ParseJson("""
                [
                  { "id": "tool-1", "role": "tool", "toolCallId": "call-1", "content": "" }
                ]
                """),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        var resumeInput = Assert.IsType<AgUiAgentResumeInput>(capturedResumeInput);
        Assert.Equal(string.Empty, resumeInput.Agent.Get<string>("latestToolResult.content"));
        Assert.False(resumeInput.Agent.TryGet<ContextObject>("latestToolResult.value", out _));
        Assert.Equal(string.Empty, resumeInput.Agent.Get<string>("messages[0].content"));
    }

    [Fact]
    public async Task AgUi_controller_maps_reasoning_stream_events_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningStart,
                MessageId = "reasoning-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 2,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningMessageStart,
                MessageId = "reasoning-1",
                MessageRole = StreamMessageRole.Reasoning,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 3,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningMessageContent,
                MessageId = "reasoning-1",
                TextDelta = "Thinking harder",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 4,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningMessageEnd,
                MessageId = "reasoning-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 5,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningEnd,
                MessageId = "reasoning-1",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("RUN_STARTED", payload);
        Assert.Contains("REASONING_START", payload);
        Assert.Contains("REASONING_MESSAGE_START", payload);
        Assert.Contains("REASONING_MESSAGE_CONTENT", payload);
        Assert.Contains("REASONING_MESSAGE_END", payload);
        Assert.Contains("REASONING_END", payload);
        Assert.Contains("\"messageId\":\"reason:reasoning-1\"", payload);
        Assert.Contains("\"role\":\"reasoning\"", payload);
        Assert.Contains("RUN_FINISHED", payload);
    }

    [Fact]
    public async Task AgUi_controller_maps_developer_and_tool_text_roles_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextStart,
                MessageId = "developer-1",
                MessageRole = StreamMessageRole.Developer,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 2,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextContent,
                MessageId = "developer-1",
                TextDelta = "Developer note",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 3,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextEnd,
                MessageId = "developer-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 4,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextStart,
                MessageId = "tool-1",
                MessageRole = StreamMessageRole.Tool,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 5,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextContent,
                MessageId = "tool-1",
                TextDelta = "Tool text",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 6,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextEnd,
                MessageId = "tool-1",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("\"type\":\"TEXT_MESSAGE_START\",\"messageId\":\"developer-1\",\"role\":\"developer\"", payload);
        Assert.Contains("\"type\":\"TEXT_MESSAGE_START\",\"messageId\":\"tool-1\",\"role\":\"tool\"", payload);
        Assert.Contains("RUN_FINISHED", payload);
    }

    [Fact]
    public async Task AgUi_controller_namespaces_reasoning_message_ids_so_assistant_text_does_not_collide()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningStart,
                MessageId = "assistant-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 2,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningMessageStart,
                MessageId = "assistant-1",
                MessageRole = StreamMessageRole.Reasoning,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 3,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningMessageContent,
                MessageId = "assistant-1",
                TextDelta = "Thinking",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 4,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningMessageEnd,
                MessageId = "assistant-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 5,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ReasoningEnd,
                MessageId = "assistant-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 6,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextStart,
                MessageId = "assistant-1",
                MessageRole = StreamMessageRole.Assistant,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 7,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextContent,
                MessageId = "assistant-1",
                TextDelta = "Hello",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 8,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextEnd,
                MessageId = "assistant-1",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("\"type\":\"REASONING_MESSAGE_START\",\"messageId\":\"reason:assistant-1\"", payload);
        Assert.Contains("\"type\":\"TEXT_MESSAGE_START\",\"messageId\":\"assistant-1\",\"role\":\"assistant\"", payload);
        Assert.DoesNotContain("\"type\":\"REASONING_MESSAGE_START\",\"messageId\":\"assistant-1\"", payload);
    }

    [Fact]
    public async Task AgUi_controller_skips_silent_stream_events_but_keeps_non_silent_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextStart,
                MessageId = "user-1",
                MessageRole = StreamMessageRole.User,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 2,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextContent,
                MessageId = "user-1",
                TextDelta = "Hello",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 3,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextEnd,
                MessageId = "user-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 4,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextStart,
                MessageId = "assistant-1",
                MessageRole = StreamMessageRole.Assistant,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 5,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextContent,
                MessageId = "assistant-1",
                TextDelta = "Hi there",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 6,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextEnd,
                MessageId = "assistant-1",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                },
                streamEvent => string.Equals(streamEvent.MessageId, "user-1", StringComparison.Ordinal)
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.DoesNotContain("\"type\":\"TEXT_MESSAGE_START\",\"messageId\":\"user-1\",\"role\":\"user\"", payload);
        Assert.DoesNotContain("\"type\":\"TEXT_MESSAGE_CONTENT\",\"messageId\":\"user-1\",\"delta\":\"Hello\"", payload);
        Assert.DoesNotContain("\"type\":\"TEXT_MESSAGE_END\",\"messageId\":\"user-1\"", payload);
        Assert.Contains("\"type\":\"TEXT_MESSAGE_START\",\"messageId\":\"assistant-1\",\"role\":\"assistant\"", payload);
        Assert.Contains("\"type\":\"TEXT_MESSAGE_CONTENT\",\"messageId\":\"assistant-1\",\"delta\":\"Hi there\"", payload);
        Assert.Contains("\"type\":\"TEXT_MESSAGE_END\",\"messageId\":\"assistant-1\"", payload);
        Assert.Contains("RUN_FINISHED", payload);
    }

    [Fact]
    public async Task AgUi_controller_maps_tool_call_stream_events_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = "call-1",
                ToolCallId = "call-1",
                TextDelta = "lookup_weather",
                ParentMessageId = "assistant-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 2,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = "call-1",
                ToolCallId = "call-1",
                TextDelta = "{\"city\":\"Sydney\"}",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 3,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = "call-1",
                ToolCallId = "call-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 4,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ToolCallResult,
                MessageId = "tool-result-1",
                ToolCallId = "call-1",
                TextDelta = "Sunny",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("TOOL_CALL_START", payload);
        Assert.Contains("TOOL_CALL_ARGS", payload);
        Assert.Contains("TOOL_CALL_END", payload);
        Assert.Contains("TOOL_CALL_RESULT", payload);
        Assert.Contains("\"toolCallId\":\"call-1\"", payload);
        Assert.Contains("\"toolCallName\":\"lookup_weather\"", payload);
        Assert.Contains("\"parentMessageId\":\"assistant-1\"", payload);
        Assert.Contains("\"messageId\":\"tool:tool-result-1\"", payload);
        Assert.Contains("\"content\":\"Sunny\"", payload);
        Assert.Contains("\"role\":\"tool\"", payload);
        Assert.Contains("RUN_FINISHED", payload);
    }

    [Fact]
    public async Task AgUi_controller_namespaces_tool_result_message_ids_so_tool_messages_do_not_collide()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextStart,
                MessageId = "assistant-1",
                MessageRole = StreamMessageRole.Assistant,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 2,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextContent,
                MessageId = "assistant-1",
                TextDelta = "Hello",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 3,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextEnd,
                MessageId = "assistant-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 4,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = "call-1",
                ToolCallId = "call-1",
                TextDelta = "lookup_weather",
                ParentMessageId = "assistant-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 5,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = "call-1",
                ToolCallId = "call-1",
                TextDelta = "{\"city\":\"Sydney\"}",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 6,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = "call-1",
                ToolCallId = "call-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 7,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ToolCallResult,
                MessageId = "assistant-1",
                ToolCallId = "call-1",
                TextDelta = "Sunny",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("\"type\":\"TEXT_MESSAGE_START\",\"messageId\":\"assistant-1\",\"role\":\"assistant\"", payload);
        Assert.Contains("\"type\":\"TOOL_CALL_RESULT\",\"messageId\":\"tool:assistant-1\",\"toolCallId\":\"call-1\",\"content\":\"Sunny\",\"role\":\"tool\"", payload);
        Assert.DoesNotContain("\"type\":\"TOOL_CALL_RESULT\",\"messageId\":\"assistant-1\"", payload);
    }

    [Fact]
    public async Task AgUi_controller_maps_step_stream_events_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.StepStart,
                TextDelta = "Search",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 2,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.StepEnd,
                TextDelta = "Search",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("\"type\":\"STEP_STARTED\",\"stepName\":\"Search\"", payload);
        Assert.Contains("\"type\":\"STEP_FINISHED\",\"stepName\":\"Search\"", payload);
    }

    [Fact]
    public async Task AgUi_controller_skips_silent_step_stream_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.StepStart,
                TextDelta = "Search",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                },
                streamEvent => streamEvent.EventKind == StreamEventKind.StepStart
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.DoesNotContain("STEP_STARTED", payload);
        Assert.Contains("RUN_FINISHED", payload);
    }

    [Fact]
    public async Task AgUi_controller_maps_activity_snapshot_stream_events_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ActivitySnapshot,
                MessageId = "activity-1",
                ActivityType = "PLAN",
                Replace = false,
                TextDelta = """{"steps":[{"title":"Search","status":"in_progress"}]}""",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("ACTIVITY_SNAPSHOT", payload);
        Assert.Contains("\"messageId\":\"activity:activity-1\"", payload);
        Assert.Contains("\"activityType\":\"PLAN\"", payload);
        Assert.Contains("\"replace\":false", payload);
        Assert.Contains("\"title\":\"Search\"", payload);
        Assert.Contains("\"status\":\"in_progress\"", payload);
    }

    [Fact]
    public async Task AgUi_controller_maps_activity_delta_stream_events_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ActivityDelta,
                MessageId = "activity-1",
                ActivityType = "PLAN",
                TextDelta = """[{"op":"replace","path":"/steps/0/status","value":"done"}]""",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("ACTIVITY_DELTA", payload);
        Assert.Contains("\"messageId\":\"activity:activity-1\"", payload);
        Assert.Contains("\"activityType\":\"PLAN\"", payload);
        Assert.Contains("\"patch\":[{\"op\":\"replace\",\"path\":\"/steps/0/status\",\"value\":\"done\"}]", payload);
    }

    [Fact]
    public async Task AgUi_controller_maps_state_snapshot_stream_events_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.StateSnapshot,
                TextDelta = """{"mode":"assistant","count":1}""",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("STATE_SNAPSHOT", payload);
        Assert.Contains("\"snapshot\":{\"mode\":\"assistant\",\"count\":1}", payload);
    }

    [Fact]
    public async Task AgUi_controller_maps_state_delta_stream_events_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.StateDelta,
                TextDelta = """[{"op":"replace","path":"/mode","value":"assistant"}]""",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("STATE_DELTA", payload);
        Assert.Contains("\"delta\":[{\"op\":\"replace\",\"path\":\"/mode\",\"value\":\"assistant\"}]", payload);
    }

    [Fact]
    public async Task AgUi_controller_maps_custom_stream_events_to_protocol_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.Custom,
                TextDelta = "weather_progress",
                Metadata = "{\"stage\":\"fetch\"}",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("\"type\":\"CUSTOM\"", payload);
        Assert.Contains("\"name\":\"weather_progress\"", payload);
        Assert.Contains("\"value\":\"{\\u0022stage\\u0022:\\u0022fetch\\u0022}\"", payload);
    }

    [Fact]
    public async Task AgUi_controller_skips_silent_activity_stream_events()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ActivitySnapshot,
                MessageId = "activity-1",
                ActivityType = "PLAN",
                TextDelta = """{"steps":[{"title":"Search","status":"in_progress"}]}""",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                },
                streamEvent => string.Equals(streamEvent.MessageId, "activity-1", StringComparison.Ordinal)
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.DoesNotContain("ACTIVITY_SNAPSHOT", payload);
        Assert.Contains("RUN_FINISHED", payload);
    }

    [Fact]
    public async Task AgUi_controller_namespaces_activity_message_ids_so_activity_messages_do_not_collide()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .ReturnsAsync(engineRunId);

        var streamEvents = new List<StreamEvent>()
        {
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 1,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextStart,
                MessageId = "assistant-1",
                MessageRole = StreamMessageRole.Assistant,
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 2,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextContent,
                MessageId = "assistant-1",
                TextDelta = "Hello",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 3,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.TextEnd,
                MessageId = "assistant-1",
            },
            new()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = engineRunId,
                WorkflowId = workflowId,
                SequenceNumber = 4,
                Created = DateTime.UtcNow,
                EventKind = StreamEventKind.ActivitySnapshot,
                MessageId = "assistant-1",
                ActivityType = "PLAN",
                TextDelta = """{"steps":[{"title":"Search","status":"in_progress"}]}""",
            },
        };

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                streamEvents,
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Hello" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        httpContext.Response.Body.Position = 0;
        var payload = Encoding.UTF8.GetString(((MemoryStream)httpContext.Response.Body).ToArray());

        Assert.Contains("\"type\":\"TEXT_MESSAGE_START\",\"messageId\":\"assistant-1\",\"role\":\"assistant\"", payload);
        Assert.Contains("\"type\":\"ACTIVITY_SNAPSHOT\",\"messageId\":\"activity:assistant-1\",\"activityType\":\"PLAN\"", payload);
        Assert.DoesNotContain("\"type\":\"ACTIVITY_SNAPSHOT\",\"messageId\":\"assistant-1\"", payload);
    }

    [Fact]
    public async Task AgUi_controller_starts_normal_workflow_run_for_non_conversation_workflows_and_populates_input_chat()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        ContextObject? capturedContext = null;

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: false));

        engineService
            .Setup(service => service.StartWorkflowRunAndNotify(
                workflowId,
                It.IsAny<ContextObject?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false
            ))
            .Callback<Guid, ContextObject?, ContextEntryListEntity?, bool>((_, context, _, _) => capturedContext = context)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = CreateHttpContext(provider);
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            Messages = ParseJson(
                """
                [
                  { "id": "system-1", "role": "system", "content": "System prompt" },
                  { "id": "developer-1", "role": "developer", "name": "planner", "content": "Developer prompt" },
                  { "id": "user-1", "role": "user", "content": "Hello" },
                  { "id": "reason-1", "role": "reasoning", "content": "Ignore me" },
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
                  { "id": "activity-1", "role": "activity", "content": { "ignored": true } },
                  { "id": "tool-1", "role": "tool", "toolCallId": "call-1", "content": "" }
                ]
                """
            ),
            State = ParseJson("""{ "mode": "assistant" }"""),
            Context = ParseJson("""[{ "id": "ctx-1" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        engineService.Verify(
            service => service.StartWorkflowRunAndNotify(
                workflowId,
                It.IsAny<ContextObject?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false
            ),
            Times.Once
        );
        engineService.Verify(
            service => service.StartOrResumeConversationAndNotify(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>()
            ),
            Times.Never
        );

        Assert.NotNull(capturedContext);
        Assert.Equal(string.Empty, capturedContext!.Get<string>("agent.latestToolResult.content"));
        Assert.Equal("call-1", capturedContext.Get<string>("agent.latestToolResult.toolCallId"));
        Assert.Equal("assistant", capturedContext.Get<string>("agent.state.mode"));
        Assert.Equal("assistant", capturedContext.Get<string>("agent._hidden.state.mode"));
        Assert.Equal("ctx-1", capturedContext.Get<string>("agent.context[0].id"));

        var inputChat = capturedContext.Get<ContextList>("input.chat");
        Assert.Equal(5, inputChat.Count);

        var systemMessage = Assert.IsType<ChatMessage>(inputChat[0]);
        Assert.Equal(ChatRole.System, systemMessage.Role);
        Assert.Equal("system-1", systemMessage.MessageId);
        Assert.Equal("System prompt", Assert.IsType<TextContent>(systemMessage.Contents.Single()).Text);

        var developerMessage = Assert.IsType<ChatMessage>(inputChat[1]);
        Assert.Equal(ChatRole.System, developerMessage.Role);
        Assert.Equal("planner", developerMessage.AuthorName);
        Assert.Equal("Developer prompt", Assert.IsType<TextContent>(developerMessage.Contents.Single()).Text);

        var userMessage = Assert.IsType<ChatMessage>(inputChat[2]);
        Assert.Equal(ChatRole.User, userMessage.Role);
        Assert.Equal("Hello", Assert.IsType<TextContent>(userMessage.Contents.Single()).Text);

        var assistantMessage = Assert.IsType<ChatMessage>(inputChat[3]);
        Assert.Equal(ChatRole.Assistant, assistantMessage.Role);
        Assert.Equal(2, assistantMessage.Contents.Count);
        Assert.Equal("Calling tool", Assert.IsType<TextContent>(assistantMessage.Contents[0]).Text);

        var functionCall = Assert.IsType<FunctionCallContent>(assistantMessage.Contents[1]);
        Assert.Equal("call-1", functionCall.CallId);
        Assert.Equal("lookup_weather", functionCall.Name);
        Assert.NotNull(functionCall.Arguments);
        Assert.Equal("Sydney", functionCall.Arguments["city"]?.ToString());

        var toolMessage = Assert.IsType<ChatMessage>(inputChat[4]);
        Assert.Equal(ChatRole.Tool, toolMessage.Role);
        var functionResult = Assert.IsType<FunctionResultContent>(toolMessage.Contents.Single());
        Assert.Equal("call-1", functionResult.CallId);
        Assert.Equal(string.Empty, functionResult.Result?.ToString());
    }

    [Fact]
    public async Task AgUi_controller_keeps_latest_user_message_out_of_input_chat_for_non_conversation_workflows()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        ContextObject? capturedContext = null;

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: false));

        engineService
            .Setup(service => service.StartWorkflowRunAndNotify(
                workflowId,
                It.IsAny<ContextObject?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false
            ))
            .Callback<Guid, ContextObject?, ContextEntryListEntity?, bool>((_, context, _, _) => capturedContext = context)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = CreateHttpContext(provider);
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Current prompt" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        Assert.NotNull(capturedContext);
        Assert.Equal("Current prompt", capturedContext!.Get<string>("agent.latestUserMessage.content"));
        Assert.Empty(capturedContext.Get<ContextList>("input.chat"));
    }

    [Fact]
    public async Task AgUi_controller_does_not_append_incoming_messages_to_input_chat_for_conversation_workflows()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        NodeResumeInput? capturedResumeInput = null;

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));

        ContextList existingChat =
        [
            new ChatMessage(
                ChatRole.Assistant,
                [new FunctionCallContent("call-1", "GetGreeting", new Dictionary<string, object?>())]
            ),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", "Howdy doody!")]),
        ];
        var existingContext = new ContextObject();
        existingContext.Set("input.chat", existingChat);

        await repositoryService.UpsertConversationCheckpoint(
            new ConversationCheckpoint()
            {
                ConversationId = "thread-1",
                ResumeMode = ConversationResumeMode.StartNode,
                ContextJson = existingContext.Serialize(provider),
                ResumeStateJson = null,
                WorkflowSnapshotsJson = null,
                GosubStackJson = null,
                CheckpointCreated = DateTime.UtcNow,
                SourceRunId = null,
            }
        );

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .Callback<Guid, string, NodeResumeInput?, ContextEntryListEntity?, bool, string?>((_, _, resumeInput, _, _, _) => capturedResumeInput = resumeInput)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = CreateHttpContext(provider);
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            Messages = ParseJson(
                """
                [
                  { "id": "tool:tool-result:batch:1:1:0", "role": "tool", "toolCallId": "call-1", "content": "\"Howdy doody!\"" },
                  { "id": "user-2", "role": "user", "content": "Next prompt" }
                ]
                """
            ),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        var resumeInput = Assert.IsType<AgUiAgentResumeInput>(capturedResumeInput);
        Assert.Equal("Next prompt", resumeInput.Agent.Get<string>("latestUserMessage.content"));
        Assert.False(resumeInput.Agent.TryGet<ContextObject>("latestToolResult", out _));
        var agentMessages = resumeInput.Agent.Get<ContextList>("messages");
        Assert.Single(agentMessages);
        Assert.Equal("user-2", resumeInput.Agent.Get<string>("messages[0].id"));
        Assert.Equal("Next prompt", resumeInput.Agent.Get<string>("messages[0].content"));
    }

    [Fact]
    public async Task AgUi_controller_uses_agent_resume_input_for_conversation_workflows()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        NodeResumeInput? capturedResumeInput = null;

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));

        ContextList existingChat =
        [
            new ChatMessage(ChatRole.User, [new TextContent("Existing prompt")]) { MessageId = "user-1" },
            new ChatMessage(ChatRole.Assistant, [new TextContent("Existing answer")]) { MessageId = "assistant-1" },
        ];
        var existingContext = new ContextObject();
        existingContext.Set("existing.value", 42);
        existingContext.Set("input.chat", existingChat);

        await repositoryService.UpsertConversationCheckpoint(
            new ConversationCheckpoint()
            {
                ConversationId = "thread-1",
                ResumeMode = ConversationResumeMode.StartNode,
                ContextJson = existingContext.Serialize(provider),
                ResumeStateJson = null,
                WorkflowSnapshotsJson = null,
                GosubStackJson = null,
                CheckpointCreated = DateTime.UtcNow,
                SourceRunId = null,
            }
        );

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .Callback<Guid, string, NodeResumeInput?, ContextEntryListEntity?, bool, string?>((_, _, resumeInput, _, _, _) => capturedResumeInput = resumeInput)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = CreateHttpContext(provider);
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            Messages = ParseJson("""[{ "id": "user-2", "role": "user", "content": "New prompt" }]"""),
            State = ParseJson("""{ "mode": "assistant" }"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        engineService.Verify(
            service => service.StartWorkflowRunAndNotify(
                It.IsAny<Guid>(),
                It.IsAny<ContextObject?>(),
                It.IsAny<ContextEntryListEntity?>(),
                It.IsAny<bool>()
            ),
            Times.Never
        );

        var resumeInput = Assert.IsType<AgUiAgentResumeInput>(capturedResumeInput);
        Assert.Equal("New prompt", resumeInput.Agent.Get<string>("latestUserMessage.content"));
        Assert.Equal("assistant", resumeInput.Agent.Get<string>("state.mode"));
        Assert.Equal("assistant", resumeInput.Agent.Get<string>("_hidden.state.mode"));
        Assert.Null(resumeInput.Context);
    }

    [Fact]
    public async Task AgUi_controller_allows_agent_message_ids_to_match_input_chat_for_conversation_workflows()
    {
        var engineRunId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();
        NodeResumeInput? capturedResumeInput = null;

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));

        ContextList existingChat = [new ChatMessage(ChatRole.User, [new TextContent("Existing prompt")]) { MessageId = "user-1" }];
        var existingContext = new ContextObject();
        existingContext.Set("input.chat", existingChat);

        await repositoryService.UpsertConversationCheckpoint(
            new ConversationCheckpoint()
            {
                ConversationId = "thread-1",
                ResumeMode = ConversationResumeMode.StartNode,
                ContextJson = existingContext.Serialize(provider),
                ResumeStateJson = null,
                WorkflowSnapshotsJson = null,
                GosubStackJson = null,
                CheckpointCreated = DateTime.UtcNow,
                SourceRunId = null,
            }
        );

        engineService
            .Setup(service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ))
            .Callback<Guid, string, NodeResumeInput?, ContextEntryListEntity?, bool, string?>((_, _, resumeInput, _, _, _) => capturedResumeInput = resumeInput)
            .ReturnsAsync(engineRunId);

        broker
            .Setup(service => service.Subscribe(engineRunId, It.IsAny<CancellationToken>()))
            .Returns(CreateUpdates(
                [],
                new Run()
                {
                    RunId = engineRunId,
                    WorkflowId = workflowId,
                    Created = DateTime.UtcNow,
                    RunStatus = RunStatus.Success,
                }
            ));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = CreateHttpContext(provider);
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": "Repeated prompt" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        engineService.Verify(
            service => service.StartOrResumeConversationAndNotify(
                workflowId,
                "thread-1",
                It.IsAny<NodeResumeInput?>(),
                It.IsAny<ContextEntryListEntity?>(),
                false,
                "thread-1"
            ),
            Times.Once
        );

        var resumeInput = Assert.IsType<AgUiAgentResumeInput>(capturedResumeInput);
        Assert.Equal("Repeated prompt", resumeInput.Agent.Get<string>("latestUserMessage.content"));
    }

    [Fact]
    public async Task AgUi_controller_returns_run_error_when_message_content_is_not_supported_for_chat_conversion()
    {
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: false));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = CreateHttpContext(provider);
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "user-1", "role": "user", "content": [{ "type": "text", "text": "Hello" }] }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        engineService.Verify(
            service => service.StartWorkflowRunAndNotify(
                It.IsAny<Guid>(),
                It.IsAny<ContextObject?>(),
                It.IsAny<ContextEntryListEntity?>(),
                It.IsAny<bool>()
            ),
            Times.Never
        );

        var events = ReadSseEvents((MemoryStream)httpContext.Response.Body);
        var errorEvent = Assert.Single(events);
        Assert.Equal("RUN_ERROR", errorEvent.GetProperty("type").GetString());
        Assert.Equal("AG-UI user message content must be a string.", errorEvent.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AgUi_controller_returns_run_error_when_tool_message_is_missing_tool_call_id()
    {
        var workflowId = Guid.NewGuid();
        var engineService = new Mock<IEngineService>();
        var broker = new Mock<IAgUiRunEventBroker>();

        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: false));

        var controller = new AgUiController(engineService.Object, broker.Object);
        var httpContext = CreateHttpContext(provider);
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            Messages = ParseJson("""[{ "id": "tool-1", "role": "tool", "content": "Sunny" }]"""),
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        await controller.Post(request);

        var events = ReadSseEvents((MemoryStream)httpContext.Response.Body);
        var errorEvent = Assert.Single(events);
        Assert.Equal("RUN_ERROR", errorEvent.GetProperty("type").GetString());
        Assert.Equal("AG-UI tool messages must include a non-empty toolCallId.", errorEvent.GetProperty("message").GetString());
    }

    private static async IAsyncEnumerable<AgUiRunUpdate> CreateUpdates(List<StreamEvent> streamEvents, Run run, Func<StreamEvent, bool>? isSilent = null)
    {
        yield return AgUiRunUpdate.ForStreamEvents(ToProgressItems(streamEvents, isSilent));
        await Task.Yield();
        yield return AgUiRunUpdate.ForRun(run);
    }

    private static List<StreamEventProgressItem> ToProgressItems(IEnumerable<StreamEvent> streamEvents, Func<StreamEvent, bool>? isSilent = null)
    {
        return streamEvents
            .Select(streamEvent => new StreamEventProgressItem()
            {
                Event = streamEvent,
                Silent = isSilent?.Invoke(streamEvent) ?? false,
            })
            .ToList();
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider? requestServices = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        if (requestServices is not null)
            httpContext.RequestServices = requestServices;

        return httpContext;
    }

    private static WorkflowEntity CreateWorkflow(Guid workflowId, bool isConversationEnabled)
    {
        return new WorkflowEntity()
        {
            Version = 1,
            Id = workflowId,
            Name = $"workflow-{workflowId:N}",
            Description = "test workflow",
            Nodes = [],
            Connections = [],
            IsConversationEnabled = isConversationEnabled,
        };
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
}
