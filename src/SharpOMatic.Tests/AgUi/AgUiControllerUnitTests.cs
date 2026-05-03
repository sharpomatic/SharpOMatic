
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

    [Fact]
    public async Task AgUi_history_returns_bad_request_for_malformed_queries()
    {
        var workflowId = Guid.NewGuid();
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        await provider.GetRequiredService<IRepositoryService>().UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));

        await AssertBadRequest(null, workflowId.ToString(), null, "AG-UI threadId is required.");
        await AssertBadRequest("thread-1", null, null, "Specify exactly one of workflowId or workflowName.");
        await AssertBadRequest("thread-1", workflowId.ToString(), "workflow", "Specify exactly one of workflowId or workflowName.");
        await AssertBadRequest("thread-1", "not-a-guid", null, "AG-UI workflowId must be a valid GUID.");

        async Task AssertBadRequest(string? threadId, string? queryWorkflowId, string? queryWorkflowName, string expectedMessage)
        {
            var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
            controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

            var result = Assert.IsType<BadRequestObjectResult>(
                await controller.History(new AgUiHistoryRequest() { ThreadId = threadId, WorkflowId = queryWorkflowId, WorkflowName = queryWorkflowName })
            );
            Assert.Equal(expectedMessage, ReadMessage(result.Value));
        }
    }

    [Fact]
    public async Task AgUi_history_returns_bad_request_for_ambiguous_workflow_name()
    {
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();

        await repositoryService.UpsertWorkflow(CreateWorkflow(Guid.NewGuid(), isConversationEnabled: true, name: "Duplicate Workflow"));
        await repositoryService.UpsertWorkflow(CreateWorkflow(Guid.NewGuid(), isConversationEnabled: true, name: "Duplicate Workflow"));

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var result = Assert.IsType<BadRequestObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowName = "Duplicate Workflow" })
        );
        Assert.Equal("There is more than one matching workflow for this name.", ReadMessage(result.Value));
    }

    [Fact]
    public async Task AgUi_history_returns_not_found_for_missing_workflow_or_thread()
    {
        var workflowId = Guid.NewGuid();
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));
        await repositoryService.UpsertConversation(
            new Conversation()
            {
                ConversationId = "thread-1",
                WorkflowId = workflowId,
                Status = ConversationStatus.Created,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
            }
        );

        await AssertNotFound("thread-1", Guid.NewGuid().ToString(), null, "AG-UI workflow was not found.");
        await AssertNotFound("thread-1", null, "missing-workflow", "AG-UI workflow was not found.");
        await AssertNotFound("missing-thread", workflowId.ToString(), null, "AG-UI conversation history was not found.");

        async Task AssertNotFound(string threadId, string? queryWorkflowId, string? queryWorkflowName, string expectedMessage)
        {
            var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
            controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

            var result = Assert.IsType<NotFoundObjectResult>(
                await controller.History(new AgUiHistoryRequest() { ThreadId = threadId, WorkflowId = queryWorkflowId, WorkflowName = queryWorkflowName })
            );
            Assert.Equal(expectedMessage, ReadMessage(result.Value));
        }
    }

    [Fact]
    public async Task AgUi_history_returns_not_found_for_workflow_thread_mismatch()
    {
        var workflowId = Guid.NewGuid();
        var otherWorkflowId = Guid.NewGuid();
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));
        await repositoryService.UpsertWorkflow(CreateWorkflow(otherWorkflowId, isConversationEnabled: true));
        await repositoryService.UpsertConversation(
            new Conversation()
            {
                ConversationId = "thread-1",
                WorkflowId = otherWorkflowId,
                Status = ConversationStatus.Created,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
            }
        );

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var result = Assert.IsType<NotFoundObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowId = workflowId.ToString() })
        );
        Assert.Equal("AG-UI conversation history was not found.", ReadMessage(result.Value));
    }

    [Fact]
    public async Task AgUi_history_returns_empty_envelope_for_non_conversation_workflows()
    {
        var workflowId = Guid.NewGuid();
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        await provider.GetRequiredService<IRepositoryService>().UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: false));

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var result = Assert.IsType<OkObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowId = workflowId.ToString() })
        );
        var json = ToJsonElement(result.Value);
        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        Assert.Equal(0, json.GetProperty("messages").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("state").ValueKind);
        Assert.Equal(0, json.GetProperty("pendingFrontendTools").GetArrayLength());
    }

    [Fact]
    public async Task AgUi_history_reduces_stream_events_to_agui_messages()
    {
        var workflowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true, name: "History Workflow"));
        await repositoryService.UpsertConversation(
            new Conversation()
            {
                ConversationId = "thread-1",
                WorkflowId = workflowId,
                Status = ConversationStatus.Created,
                Created = created,
                Updated = created,
            }
        );
        await repositoryService.UpsertRun(
            new Run()
            {
                RunId = runId,
                WorkflowId = workflowId,
                ConversationId = "thread-1",
                Created = created,
                RunStatus = RunStatus.Success,
            }
        );
        await repositoryService.AppendStreamEvents(
            [
                CreateStreamEvent(runId, workflowId, "thread-1", 1, StreamEventKind.TextStart, messageId: "user-1", role: StreamMessageRole.User),
                CreateStreamEvent(runId, workflowId, "thread-1", 2, StreamEventKind.TextContent, messageId: "user-1", textDelta: "Hello"),
                CreateStreamEvent(runId, workflowId, "thread-1", 3, StreamEventKind.TextEnd, messageId: "user-1"),
                CreateStreamEvent(runId, workflowId, "thread-1", 4, StreamEventKind.TextStart, messageId: "hidden-1", role: StreamMessageRole.Assistant, hideFromReply: true),
                CreateStreamEvent(runId, workflowId, "thread-1", 5, StreamEventKind.TextStart, messageId: "assistant-1", role: StreamMessageRole.Assistant),
                CreateStreamEvent(runId, workflowId, "thread-1", 6, StreamEventKind.TextContent, messageId: "assistant-1", textDelta: "Hi"),
                CreateStreamEvent(runId, workflowId, "thread-1", 7, StreamEventKind.TextEnd, messageId: "assistant-1"),
                CreateStreamEvent(runId, workflowId, "thread-1", 8, StreamEventKind.ReasoningMessageStart, messageId: "reasoning-1", role: StreamMessageRole.Reasoning),
                CreateStreamEvent(runId, workflowId, "thread-1", 9, StreamEventKind.ReasoningMessageContent, messageId: "reasoning-1", textDelta: "Thinking"),
                CreateStreamEvent(runId, workflowId, "thread-1", 10, StreamEventKind.ReasoningMessageEnd, messageId: "reasoning-1"),
                CreateStreamEvent(runId, workflowId, "thread-1", 11, StreamEventKind.ToolCallStart, messageId: "call-1", toolCallId: "call-1", parentMessageId: "assistant-1", textDelta: "lookup_weather"),
                CreateStreamEvent(runId, workflowId, "thread-1", 12, StreamEventKind.ToolCallArgs, messageId: "call-1", toolCallId: "call-1", textDelta: "{\"city\":\"Sydney\"}"),
                CreateStreamEvent(runId, workflowId, "thread-1", 13, StreamEventKind.ToolCallEnd, messageId: "call-1", toolCallId: "call-1"),
                CreateStreamEvent(runId, workflowId, "thread-1", 14, StreamEventKind.ToolCallResult, messageId: "tool-result-1", toolCallId: "call-1", textDelta: "Sunny"),
                CreateStreamEvent(runId, workflowId, "thread-1", 15, StreamEventKind.ActivitySnapshot, messageId: "plan-1", activityType: "plan", textDelta: "{\"steps\":[{\"status\":\"todo\"}]}"),
                CreateStreamEvent(runId, workflowId, "thread-1", 16, StreamEventKind.ActivityDelta, messageId: "plan-1", activityType: "plan", textDelta: "[{\"op\":\"replace\",\"path\":\"/steps/0/status\",\"value\":\"done\"}]"),
            ]
        );

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var result = Assert.IsType<OkObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowName = "History Workflow" })
        );
        var envelope = ToJsonElement(result.Value);
        var messages = envelope.GetProperty("messages");

        Assert.Equal(5, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Hello", messages[0].GetProperty("content").GetString());

        var assistantMessage = messages[1];
        Assert.Equal("assistant", assistantMessage.GetProperty("role").GetString());
        Assert.Equal("Hi", assistantMessage.GetProperty("content").GetString());
        var toolCall = assistantMessage.GetProperty("toolCalls")[0];
        Assert.Equal("call-1", toolCall.GetProperty("id").GetString());
        Assert.Equal("lookup_weather", toolCall.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("{\"city\":\"Sydney\"}", toolCall.GetProperty("function").GetProperty("arguments").GetString());

        Assert.Equal("reason:reasoning-1", messages[2].GetProperty("id").GetString());
        Assert.Equal("reasoning", messages[2].GetProperty("role").GetString());
        Assert.Equal("Thinking", messages[2].GetProperty("content").GetString());

        Assert.Equal("tool:tool-result-1", messages[3].GetProperty("id").GetString());
        Assert.Equal("tool", messages[3].GetProperty("role").GetString());
        Assert.Equal("call-1", messages[3].GetProperty("toolCallId").GetString());
        Assert.Equal("Sunny", messages[3].GetProperty("content").GetString());

        Assert.Equal("activity:plan-1", messages[4].GetProperty("id").GetString());
        Assert.Equal("activity", messages[4].GetProperty("role").GetString());
        Assert.Equal("plan", messages[4].GetProperty("activityType").GetString());
        Assert.Equal("done", messages[4].GetProperty("content").GetProperty("steps")[0].GetProperty("status").GetString());

        Assert.DoesNotContain(messages.EnumerateArray(), message => message.GetProperty("id").GetString() == "hidden-1");
        Assert.Equal(JsonValueKind.Null, envelope.GetProperty("state").ValueKind);
        Assert.Equal(0, envelope.GetProperty("pendingFrontendTools").GetArrayLength());
    }

    [Fact]
    public async Task AgUi_history_keeps_tool_results_when_provider_reuses_result_message_id()
    {
        var workflowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));
        await repositoryService.UpsertConversation(CreateConversation("thread-1", workflowId, created));
        await repositoryService.UpsertRun(CreateRun(runId, workflowId, "thread-1", created));
        await repositoryService.AppendStreamEvents(
            [
                CreateStreamEvent(runId, workflowId, "thread-1", 1, StreamEventKind.ToolCallStart, messageId: "call-1", toolCallId: "call-1", parentMessageId: "assistant-1", textDelta: "GetGreeting"),
                CreateStreamEvent(runId, workflowId, "thread-1", 2, StreamEventKind.ToolCallArgs, messageId: "call-1", toolCallId: "call-1", textDelta: "{}"),
                CreateStreamEvent(runId, workflowId, "thread-1", 3, StreamEventKind.ToolCallResult, messageId: "shared-result", toolCallId: "call-1", textDelta: "\"Howdy doody!\""),
                CreateStreamEvent(runId, workflowId, "thread-1", 4, StreamEventKind.ToolCallStart, messageId: "call-2", toolCallId: "call-2", parentMessageId: "assistant-1", textDelta: "GetTime"),
                CreateStreamEvent(runId, workflowId, "thread-1", 5, StreamEventKind.ToolCallArgs, messageId: "call-2", toolCallId: "call-2", textDelta: "{}"),
                CreateStreamEvent(runId, workflowId, "thread-1", 6, StreamEventKind.ToolCallResult, messageId: "shared-result", toolCallId: "call-2", textDelta: "\"3/05/2026 3:31:12 PM +10:00\""),
            ]
        );

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var result = Assert.IsType<OkObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowId = workflowId.ToString() })
        );
        var messages = ToJsonElement(result.Value).GetProperty("messages");
        var toolResults = messages.EnumerateArray().Where(message => message.GetProperty("role").GetString() == "tool").ToList();

        Assert.Equal(2, toolResults.Count);
        Assert.Equal("tool:shared-result", toolResults[0].GetProperty("id").GetString());
        Assert.Equal("call-1", toolResults[0].GetProperty("toolCallId").GetString());
        Assert.Equal("\"Howdy doody!\"", toolResults[0].GetProperty("content").GetString());
        Assert.Equal("tool:shared-result:call-2", toolResults[1].GetProperty("id").GetString());
        Assert.Equal("call-2", toolResults[1].GetProperty("toolCallId").GetString());
        Assert.Equal("\"3/05/2026 3:31:12 PM +10:00\"", toolResults[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task AgUi_history_prefers_checkpoint_agent_state()
    {
        var workflowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));
        await repositoryService.UpsertConversation(CreateConversation("thread-1", workflowId, created));
        await repositoryService.UpsertRun(CreateRun(runId, workflowId, "thread-1", created));
        await repositoryService.AppendStreamEvents(
            [
                CreateStreamEvent(runId, workflowId, "thread-1", 1, StreamEventKind.StateSnapshot, textDelta: "{\"mode\":\"stream\"}"),
            ]
        );

        var checkpointContext = new ContextObject();
        checkpointContext.Set("agent.state.mode", "checkpoint");
        checkpointContext.Set("agent.state.count", 7);
        await repositoryService.UpsertConversationCheckpoint(CreateCheckpoint("thread-1", checkpointContext.Serialize(provider), created));

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var result = Assert.IsType<OkObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowId = workflowId.ToString() })
        );
        var state = ToJsonElement(result.Value).GetProperty("state");
        Assert.Equal("checkpoint", state.GetProperty("mode").GetString());
        Assert.Equal(7, state.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task AgUi_history_falls_back_to_state_stream_events()
    {
        var workflowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));
        await repositoryService.UpsertConversation(CreateConversation("thread-1", workflowId, created));
        await repositoryService.UpsertRun(CreateRun(runId, workflowId, "thread-1", created));
        await repositoryService.AppendStreamEvents(
            [
                CreateStreamEvent(runId, workflowId, "thread-1", 1, StreamEventKind.StateSnapshot, textDelta: "{\"mode\":\"stream\",\"count\":1}"),
                CreateStreamEvent(runId, workflowId, "thread-1", 2, StreamEventKind.StateDelta, textDelta: "[{\"op\":\"replace\",\"path\":\"/mode\",\"value\":\"patched\"},{\"op\":\"add\",\"path\":\"/extra\",\"value\":true}]"),
            ]
        );

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var result = Assert.IsType<OkObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowId = workflowId.ToString() })
        );
        var state = ToJsonElement(result.Value).GetProperty("state");
        Assert.Equal("patched", state.GetProperty("mode").GetString());
        Assert.Equal(1, state.GetProperty("count").GetInt32());
        Assert.True(state.GetProperty("extra").GetBoolean());
    }

    [Fact]
    public async Task AgUi_history_returns_pending_frontend_tool_only_when_last_message_is_unresolved_frontend_call()
    {
        var workflowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));
        await repositoryService.UpsertConversation(CreateConversation("thread-1", workflowId, created));
        await repositoryService.UpsertRun(CreateRun(runId, workflowId, "thread-1", created));
        await repositoryService.AppendStreamEvents(
            [
                CreateStreamEvent(runId, workflowId, "thread-1", 1, StreamEventKind.TextStart, messageId: "user-1", role: StreamMessageRole.User),
                CreateStreamEvent(runId, workflowId, "thread-1", 2, StreamEventKind.TextContent, messageId: "user-1", textDelta: "Confirm?"),
                CreateStreamEvent(runId, workflowId, "thread-1", 3, StreamEventKind.ToolCallStart, messageId: "tool-1", toolCallId: "tool-1", parentMessageId: "frontend-tool-call:tool-1", textDelta: "ask_a_question"),
                CreateStreamEvent(runId, workflowId, "thread-1", 4, StreamEventKind.ToolCallArgs, messageId: "tool-1", toolCallId: "tool-1", textDelta: "{\"title\":\"Continue\",\"message\":\"Proceed?\"}"),
                CreateStreamEvent(runId, workflowId, "thread-1", 5, StreamEventKind.ToolCallEnd, messageId: "tool-1", toolCallId: "tool-1"),
            ]
        );

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var result = Assert.IsType<OkObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowId = workflowId.ToString() })
        );
        var pending = ToJsonElement(result.Value).GetProperty("pendingFrontendTools");
        var pendingTool = Assert.Single(pending.EnumerateArray());
        Assert.Equal("tool-1", pendingTool.GetProperty("toolCallId").GetString());
        Assert.Equal("ask_a_question", pendingTool.GetProperty("toolName").GetString());
        Assert.Equal("{\"title\":\"Continue\",\"message\":\"Proceed?\"}", pendingTool.GetProperty("argumentsJson").GetString());
        Assert.Equal("frontend-tool-call:tool-1", pendingTool.GetProperty("assistantMessageId").GetString());
    }

    [Fact]
    public async Task AgUi_history_omits_pending_frontend_tool_when_resolved_or_not_last_message()
    {
        var workflowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        using var provider = SharpOMatic.Tests.Workflows.WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(CreateWorkflow(workflowId, isConversationEnabled: true));
        await repositoryService.UpsertConversation(CreateConversation("thread-1", workflowId, created));
        await repositoryService.UpsertConversation(CreateConversation("thread-2", workflowId, created));
        await repositoryService.UpsertRun(CreateRun(runId, workflowId, "thread-1", created));
        await repositoryService.AppendStreamEvents(
            [
                CreateStreamEvent(runId, workflowId, "thread-1", 1, StreamEventKind.ToolCallStart, messageId: "tool-1", toolCallId: "tool-1", parentMessageId: "frontend-tool-call:tool-1", textDelta: "ask_a_question"),
                CreateStreamEvent(runId, workflowId, "thread-1", 2, StreamEventKind.ToolCallArgs, messageId: "tool-1", toolCallId: "tool-1", textDelta: "{}"),
                CreateStreamEvent(runId, workflowId, "thread-1", 3, StreamEventKind.ToolCallResult, messageId: "result-1", toolCallId: "tool-1", textDelta: "true"),
                CreateStreamEvent(runId, workflowId, "thread-2", 1, StreamEventKind.ToolCallStart, messageId: "tool-2", toolCallId: "tool-2", parentMessageId: "frontend-tool-call:tool-2", textDelta: "ask_a_question"),
                CreateStreamEvent(runId, workflowId, "thread-2", 2, StreamEventKind.ToolCallArgs, messageId: "tool-2", toolCallId: "tool-2", textDelta: "{}"),
                CreateStreamEvent(runId, workflowId, "thread-2", 3, StreamEventKind.TextStart, messageId: "assistant-2", role: StreamMessageRole.Assistant),
                CreateStreamEvent(runId, workflowId, "thread-2", 4, StreamEventKind.TextContent, messageId: "assistant-2", textDelta: "after"),
            ]
        );

        var controller = new AgUiController(new Mock<IEngineService>().Object, new Mock<IAgUiRunEventBroker>().Object);
        controller.ControllerContext = new ControllerContext() { HttpContext = CreateHttpContext(provider) };

        var resolvedResult = Assert.IsType<OkObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-1", WorkflowId = workflowId.ToString() })
        );
        Assert.Equal(0, ToJsonElement(resolvedResult.Value).GetProperty("pendingFrontendTools").GetArrayLength());

        var notLastResult = Assert.IsType<OkObjectResult>(
            await controller.History(new AgUiHistoryRequest() { ThreadId = "thread-2", WorkflowId = workflowId.ToString() })
        );
        Assert.Equal(0, ToJsonElement(notLastResult.Value).GetProperty("pendingFrontendTools").GetArrayLength());
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

    private static Conversation CreateConversation(string conversationId, Guid workflowId, DateTime created)
    {
        return new Conversation()
        {
            ConversationId = conversationId,
            WorkflowId = workflowId,
            Status = ConversationStatus.Created,
            Created = created,
            Updated = created,
        };
    }

    private static Run CreateRun(Guid runId, Guid workflowId, string conversationId, DateTime created)
    {
        return new Run()
        {
            RunId = runId,
            WorkflowId = workflowId,
            ConversationId = conversationId,
            Created = created,
            RunStatus = RunStatus.Success,
        };
    }

    private static ConversationCheckpoint CreateCheckpoint(string conversationId, string contextJson, DateTime created)
    {
        return new ConversationCheckpoint()
        {
            ConversationId = conversationId,
            ResumeMode = ConversationResumeMode.StartNode,
            ContextJson = contextJson,
            CheckpointCreated = created,
        };
    }

    private static WorkflowEntity CreateWorkflow(Guid workflowId, bool isConversationEnabled, string? name = null)
    {
        return new WorkflowEntity()
        {
            Version = 1,
            Id = workflowId,
            Name = name ?? $"workflow-{workflowId:N}",
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

    private static StreamEvent CreateStreamEvent(
        Guid runId,
        Guid workflowId,
        string conversationId,
        int sequenceNumber,
        StreamEventKind kind,
        string? messageId = null,
        StreamMessageRole? role = null,
        string? activityType = null,
        string? textDelta = null,
        string? toolCallId = null,
        string? parentMessageId = null,
        bool hideFromReply = false
    )
    {
        return new StreamEvent()
        {
            StreamEventId = Guid.NewGuid(),
            RunId = runId,
            WorkflowId = workflowId,
            ConversationId = conversationId,
            SequenceNumber = sequenceNumber,
            Created = DateTime.UtcNow.AddMilliseconds(sequenceNumber),
            EventKind = kind,
            MessageId = messageId,
            MessageRole = role,
            ActivityType = activityType,
            TextDelta = textDelta,
            ToolCallId = toolCallId,
            ParentMessageId = parentMessageId,
            HideFromReply = hideFromReply,
        };
    }

    private static JsonElement ToJsonElement(object? value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return document.RootElement.Clone();
    }

    private static string? ReadMessage(object? value)
    {
        var json = ToJsonElement(value);
        return json.TryGetProperty("message", out var message) ? message.GetString() : null;
    }
}
