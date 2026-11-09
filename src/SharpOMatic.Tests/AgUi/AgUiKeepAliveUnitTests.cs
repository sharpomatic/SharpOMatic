namespace SharpOMatic.Tests.AgUi;

public sealed class AgUiKeepAliveUnitTests
{
    private const string KeepAliveComment = ": keepalive";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AgUi_controller_writes_keep_alive_comments_while_the_workflow_is_idle()
    {
        var gate = new TaskCompletionSource();
        var fixture = CreateFixture(TimeSpan.FromMilliseconds(40), (runId, workflowId) => GatedUpdates(gate.Task, SuccessRun(runId, workflowId)));

        var post = fixture.Controller.Post(fixture.Request);
        await WaitForKeepAlivesAsync(fixture.Body, 2);
        gate.SetResult();
        await post.WaitAsync(TestTimeout);

        var frames = ReadFrames(fixture.Body);
        Assert.True(frames.Count(frame => frame == KeepAliveComment) >= 2, $"Expected at least two keep-alive comments but found: {string.Join(" | ", frames)}");
        Assert.Equal("RUN_STARTED", EventType(frames[0]));
        Assert.Equal("RUN_FINISHED", EventType(frames[^1]));
        Assert.All(frames[1..^1], frame => Assert.Equal(KeepAliveComment, frame));
    }

    [Fact]
    public async Task AgUi_controller_keep_alive_is_a_comment_that_adds_no_events()
    {
        var gate = new TaskCompletionSource();
        var fixture = CreateFixture(TimeSpan.FromMilliseconds(40), (runId, workflowId) => GatedUpdates(gate.Task, SuccessRun(runId, workflowId)));

        var post = fixture.Controller.Post(fixture.Request);
        await WaitForKeepAlivesAsync(fixture.Body, 1);
        gate.SetResult();
        await post.WaitAsync(TestTimeout);

        var payload = ReadPayload(fixture.Body);
        Assert.Contains(": keepalive\n\n", payload);
        Assert.DoesNotContain("\r", payload);

        var dataFrames = ReadFrames(fixture.Body).Where(frame => frame.StartsWith("data:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[] { "RUN_STARTED", "RUN_FINISHED" }, dataFrames.Select(EventType).ToArray());
    }

    [Fact]
    public async Task AgUi_controller_writes_keep_alive_when_only_silent_updates_arrive()
    {
        var fixture = CreateFixture(
            TimeSpan.FromMilliseconds(80),
            (runId, workflowId) => TimedUpdates(runId, workflowId, count: 20, every: TimeSpan.FromMilliseconds(20), silent: true, SuccessRun(runId, workflowId))
        );

        await fixture.Controller.Post(fixture.Request).WaitAsync(TestTimeout);

        var frames = ReadFrames(fixture.Body);
        Assert.True(frames.Count(frame => frame == KeepAliveComment) >= 1, $"Silent updates write nothing, so a keep-alive was expected but found: {string.Join(" | ", frames)}");
        var dataFrames = frames.Where(frame => frame.StartsWith("data:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(new[] { "RUN_STARTED", "RUN_FINISHED" }, dataFrames.Select(EventType).ToArray());
    }

    [Fact]
    public async Task AgUi_controller_does_not_write_keep_alive_while_events_are_flowing()
    {
        var fixture = CreateFixture(
            TimeSpan.FromSeconds(5),
            (runId, workflowId) => TimedUpdates(runId, workflowId, count: 15, every: TimeSpan.FromMilliseconds(20), silent: false, SuccessRun(runId, workflowId))
        );

        await fixture.Controller.Post(fixture.Request).WaitAsync(TestTimeout);

        var frames = ReadFrames(fixture.Body);
        Assert.DoesNotContain(KeepAliveComment, frames);
        Assert.Equal("RUN_STARTED", EventType(frames[0]));
        Assert.Equal("RUN_FINISHED", EventType(frames[^1]));
        Assert.Equal(15, frames.Count(frame => EventType(frame) == "TEXT_MESSAGE_CONTENT"));
    }

    [Fact]
    public async Task AgUi_controller_writes_nothing_after_run_finished()
    {
        var gate = new TaskCompletionSource();
        var fixture = CreateFixture(TimeSpan.FromMilliseconds(40), (runId, workflowId) => GatedUpdates(gate.Task, SuccessRun(runId, workflowId)));

        var post = fixture.Controller.Post(fixture.Request);
        await WaitForKeepAlivesAsync(fixture.Body, 1);
        gate.SetResult();
        await post.WaitAsync(TestTimeout);
        var lengthAtCompletion = fixture.Body.Length;

        await Task.Delay(300);

        Assert.Equal(lengthAtCompletion, fixture.Body.Length);
        Assert.Equal("RUN_FINISHED", EventType(ReadFrames(fixture.Body)[^1]));
    }

    [Fact]
    public async Task AgUi_controller_writes_nothing_after_run_error()
    {
        var gate = new TaskCompletionSource();
        var fixture = CreateFixture(
            TimeSpan.FromMilliseconds(40),
            (runId, workflowId) => GatedUpdates(gate.Task, new Run() { RunId = runId, WorkflowId = workflowId, Created = DateTime.UtcNow, RunStatus = RunStatus.Failed, Error = "Boom" })
        );

        var post = fixture.Controller.Post(fixture.Request);
        await WaitForKeepAlivesAsync(fixture.Body, 1);
        gate.SetResult();
        await post.WaitAsync(TestTimeout);
        var lengthAtCompletion = fixture.Body.Length;

        await Task.Delay(300);

        Assert.Equal(lengthAtCompletion, fixture.Body.Length);
        Assert.Equal("RUN_ERROR", EventType(ReadFrames(fixture.Body)[^1]));
    }

    [Fact]
    public async Task AgUi_controller_does_not_write_keep_alive_for_a_short_run_with_the_default_interval()
    {
        var fixture = CreateFixture(keepAliveInterval: null, (runId, workflowId) => TimedUpdates(runId, workflowId, count: 3, every: TimeSpan.FromMilliseconds(20), silent: false, SuccessRun(runId, workflowId)));

        await fixture.Controller.Post(fixture.Request).WaitAsync(TestTimeout);

        Assert.DoesNotContain(KeepAliveComment, ReadFrames(fixture.Body));
    }

    [Fact]
    public async Task AgUi_controller_disposes_the_update_stream_when_the_client_disconnects_while_idle()
    {
        var gate = new TaskCompletionSource();
        var disposed = false;
        using var aborted = new CancellationTokenSource();
        var fixture = CreateFixture(
            TimeSpan.FromMilliseconds(40),
            (runId, workflowId) => GatedUpdates(gate.Task, SuccessRun(runId, workflowId), () => disposed = true),
            aborted.Token
        );

        var post = fixture.Controller.Post(fixture.Request);
        await WaitForKeepAlivesAsync(fixture.Body, 1);
        await aborted.CancelAsync();
        await post.WaitAsync(TestTimeout);

        Assert.True(disposed);
        var frames = ReadFrames(fixture.Body);
        Assert.Equal("RUN_STARTED", EventType(frames[0]));
        Assert.DoesNotContain(frames, frame => frame.StartsWith("data:", StringComparison.Ordinal) && EventType(frame) is "RUN_FINISHED" or "RUN_ERROR");
    }

    [Fact]
    public void AddSharpOMaticAgUi_defaults_the_keep_alive_interval_to_fifteen_seconds()
    {
        var services = new ServiceCollection();
        services.AddSharpOMaticAgUi();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(TimeSpan.FromSeconds(15), provider.GetRequiredService<IOptions<AgUiOptions>>().Value.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), new AgUiOptions().KeepAliveInterval);
    }

    [Fact]
    public void AddSharpOMaticAgUi_applies_a_configured_keep_alive_interval()
    {
        var services = new ServiceCollection();
        services.AddSharpOMaticAgUi(options => options.KeepAliveInterval = TimeSpan.FromSeconds(5), "/custom");
        using var provider = services.BuildServiceProvider();

        Assert.Equal(TimeSpan.FromSeconds(5), provider.GetRequiredService<IOptions<AgUiOptions>>().Value.KeepAliveInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5000)]
    [InlineData(2_160_000_000)]
    public void AddSharpOMaticAgUi_rejects_a_keep_alive_interval_outside_the_valid_range(long milliseconds)
    {
        var services = new ServiceCollection();
        services.AddSharpOMaticAgUi(options => options.KeepAliveInterval = TimeSpan.FromMilliseconds(milliseconds));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AgUiOptions>>().Value);
    }

    [Fact]
    public async Task AddSharpOMaticAgUi_rejects_an_invalid_keep_alive_interval_when_the_host_starts()
    {
        using var host = new Microsoft.Extensions.Hosting.HostBuilder()
            .ConfigureServices((_, services) => services.AddSharpOMaticAgUi(options => options.KeepAliveInterval = TimeSpan.Zero))
            .Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    private sealed record Fixture(AgUiController Controller, AgUiRunRequest Request, SynchronizedBody Body);

    private sealed class SynchronizedBody : MemoryStream
    {
        private readonly object _gate = new();

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_gate)
                base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            lock (_gate)
                base.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            lock (_gate)
                base.WriteByte(value);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public byte[] Snapshot()
        {
            lock (_gate)
                return ToArray();
        }
    }

    private static Fixture CreateFixture(
        TimeSpan? keepAliveInterval,
        Func<Guid, Guid, IAsyncEnumerable<AgUiRunUpdate>> createUpdates,
        CancellationToken requestAborted = default
    )
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
            .Returns(createUpdates(engineRunId, workflowId));

        var options = keepAliveInterval is null ? null : Options.Create(new AgUiOptions() { KeepAliveInterval = keepAliveInterval.Value });
        var controller = new AgUiController(engineService.Object, broker.Object, null, options);
        var body = new SynchronizedBody();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = body;
        httpContext.RequestAborted = requestAborted;
        controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

        var request = new AgUiRunRequest()
        {
            ThreadId = "thread-1",
            RunId = "protocol-run-1",
            ForwardedProps = ParseJson($$"""{"workflowId":"{{workflowId}}"}"""),
        };

        return new Fixture(controller, request, body);
    }

    private static Run SuccessRun(Guid runId, Guid workflowId)
    {
        return new Run()
        {
            RunId = runId,
            WorkflowId = workflowId,
            Created = DateTime.UtcNow,
            RunStatus = RunStatus.Success,
        };
    }

    private static async IAsyncEnumerable<AgUiRunUpdate> GatedUpdates(Task gate, Run run, Action? onDisposed = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            await gate.WaitAsync(cancellationToken);
            yield return AgUiRunUpdate.ForRun(run);
        }
        finally
        {
            onDisposed?.Invoke();
        }
    }

    private static async IAsyncEnumerable<AgUiRunUpdate> TimedUpdates(Guid runId, Guid workflowId, int count, TimeSpan every, bool silent, Run finalRun)
    {
        for (var index = 0; index < count; index++)
        {
            await Task.Delay(every);
            yield return AgUiRunUpdate.ForStreamEvents(
                [
                    new StreamEventProgressItem()
                    {
                        Event = new StreamEvent()
                        {
                            StreamEventId = Guid.NewGuid(),
                            RunId = runId,
                            WorkflowId = workflowId,
                            ConversationId = "thread-1",
                            SequenceNumber = index + 1,
                            Created = DateTime.UtcNow,
                            EventKind = StreamEventKind.TextContent,
                            MessageId = "message-1",
                            TextDelta = "x",
                        },
                        Silent = silent,
                    },
                ]
            );
        }

        yield return AgUiRunUpdate.ForRun(finalRun);
    }

    private static string ReadPayload(SynchronizedBody body)
    {
        return Encoding.UTF8.GetString(body.Snapshot());
    }

    private static string[] ReadFrames(SynchronizedBody body)
    {
        return ReadPayload(body).Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
    }

    private static async Task WaitForKeepAlivesAsync(SynchronizedBody body, int count)
    {
        var deadline = DateTime.UtcNow + TestTimeout;
        while (ReadFrames(body).Count(frame => frame == KeepAliveComment) < count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {count} keep-alive comment(s); output so far: {ReadPayload(body)}");
            await Task.Delay(10);
        }
    }

    private static string EventType(string frame)
    {
        if (!frame.StartsWith("data: ", StringComparison.Ordinal))
            return string.Empty;

        using var document = JsonDocument.Parse(frame["data: ".Length..]);
        return document.RootElement.GetProperty("type").GetString()!;
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
