
namespace SharpOMatic.Tests.AgUi;

public sealed class AgUiControllerUnitTests
{
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
        Assert.Equal("Hi", resumeInput.Agent.Get<string>("messages[1].content"));
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
        Assert.Equal("{\"status\":\"Sunny\",\"temperatureC\":24}", resumeInput.Agent.Get<string>("messages[1].content"));
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
}
