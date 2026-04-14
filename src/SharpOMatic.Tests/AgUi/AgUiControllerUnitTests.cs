using Moq;

namespace SharpOMatic.Tests.AgUi;

public sealed class AgUiControllerUnitTests
{
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
        Assert.Contains("\"role\":\"reasoning\"", payload);
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
        Assert.Contains("\"messageId\":\"tool-result-1\"", payload);
        Assert.Contains("\"content\":\"Sunny\"", payload);
        Assert.Contains("\"role\":\"tool\"", payload);
        Assert.Contains("RUN_FINISHED", payload);
    }

    private static async IAsyncEnumerable<AgUiRunUpdate> CreateUpdates(List<StreamEvent> streamEvents, Run run)
    {
        yield return AgUiRunUpdate.ForStreamEvents(streamEvents);
        await Task.Yield();
        yield return AgUiRunUpdate.ForRun(run);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
