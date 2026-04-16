
namespace SharpOMatic.Tests.Services;

public sealed class EditorProgressServiceUnitTests
{
    [Fact]
    public async Task Editor_progress_service_forwards_plain_stream_events_and_ignores_transient_silent_flag()
    {
        var streamEvent = new StreamEvent()
        {
            StreamEventId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            SequenceNumber = 1,
            Created = DateTime.UtcNow,
            EventKind = StreamEventKind.TextContent,
            MessageId = "message-1",
            TextDelta = "Hello",
        };

        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(proxy => proxy.SendCoreAsync(
                "StreamEventProgress",
                It.Is<object?[]>(args => IsExpectedStreamEventPayload(args, streamEvent)),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(clients => clients.All).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(hubClients.Object);

        var progressService = new ProgressService(hubContext.Object);
        var run = new Run()
        {
            RunId = streamEvent.RunId,
            WorkflowId = streamEvent.WorkflowId,
            Created = DateTime.UtcNow,
            NeedsEditorEvents = true,
            RunStatus = RunStatus.Running,
        };

        await progressService.StreamEventProgress(
            run,
            [new StreamEventProgressItem() { Event = streamEvent, Silent = true }]
        );

        clientProxy.Verify(proxy => proxy.SendCoreAsync(
            "StreamEventProgress",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()
        ));
    }

    private static bool IsExpectedStreamEventPayload(object?[] args, StreamEvent expectedEvent)
    {
        if ((args.Length != 1) || (args[0] is not List<StreamEvent> events) || (events.Count != 1))
            return false;

        var actualEvent = events[0];
        return (actualEvent.StreamEventId == expectedEvent.StreamEventId)
            && (actualEvent.EventKind == expectedEvent.EventKind)
            && string.Equals(actualEvent.MessageId, expectedEvent.MessageId, StringComparison.Ordinal)
            && string.Equals(actualEvent.TextDelta, expectedEvent.TextDelta, StringComparison.Ordinal);
    }
}
