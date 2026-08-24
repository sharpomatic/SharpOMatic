namespace SharpOMatic.Tests.Workflows;

public sealed class EvalAgUiOutputUnitTests
{
    private const string DefaultPath = "agui.messages";

    [Fact]
    public async Task Ag_ui_messages_are_added_at_the_default_path_when_no_path_is_configured()
    {
        var runId = Guid.NewGuid();
        var repository = CreateRepositoryWithRun(runId);
        ContextObject graderContext = [];

        await InvokeAddAgUiOutput(repository, CreateEvalConfig(includeAgUiOutput: true), runId, graderContext);

        var message = RequireSingleMessage(graderContext, DefaultPath);
        Assert.Equal("assistant", message.GetProperty("role").GetString());
        Assert.Equal("Hello there", message.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Ag_ui_messages_are_added_at_the_configured_path()
    {
        var runId = Guid.NewGuid();
        var repository = CreateRepositoryWithRun(runId);
        ContextObject graderContext = [];

        await InvokeAddAgUiOutput(repository, CreateEvalConfig(includeAgUiOutput: true, agUiOutputPath: "run.agui"), runId, graderContext);

        Assert.Equal(1, MessageCount(graderContext, "run.agui"));
        Assert.False(graderContext.TryGet<object?>(DefaultPath, out _));
    }

    [Fact]
    public async Task Configured_path_is_trimmed_before_use()
    {
        var runId = Guid.NewGuid();
        var repository = CreateRepositoryWithRun(runId);
        ContextObject graderContext = [];

        await InvokeAddAgUiOutput(repository, CreateEvalConfig(includeAgUiOutput: true, agUiOutputPath: "  run.agui  "), runId, graderContext);

        Assert.Equal(1, MessageCount(graderContext, "run.agui"));
    }

    [Fact]
    public async Task Nothing_is_added_when_the_evaluation_does_not_ask_for_ag_ui_output()
    {
        var runId = Guid.NewGuid();
        var repository = CreateRepositoryWithRun(runId);
        ContextObject graderContext = [];

        await InvokeAddAgUiOutput(repository, CreateEvalConfig(includeAgUiOutput: false), runId, graderContext);

        Assert.Empty(graderContext);
    }

    [Fact]
    public async Task Events_hidden_from_reply_are_excluded()
    {
        var runId = Guid.NewGuid();
        var repository = new TestRepositoryService();
        await repository.AppendStreamEvents([
            AgUiMessageBuilderUnitTests.CreateEvent(1, StreamEventKind.TextStart, messageId: "m1", messageRole: StreamMessageRole.Assistant, runId: runId),
            AgUiMessageBuilderUnitTests.CreateEvent(2, StreamEventKind.TextContent, messageId: "m1", textDelta: "kept", runId: runId),
            AgUiMessageBuilderUnitTests.CreateEvent(3, StreamEventKind.TextContent, messageId: "m1", textDelta: "-hidden", runId: runId, hideFromReply: true),
        ]);

        ContextObject graderContext = [];
        await InvokeAddAgUiOutput(repository, CreateEvalConfig(includeAgUiOutput: true), runId, graderContext);

        var message = RequireSingleMessage(graderContext, DefaultPath);
        Assert.Equal("kept", message.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Events_from_other_runs_are_excluded()
    {
        var runId = Guid.NewGuid();
        var repository = CreateRepositoryWithRun(runId);
        await repository.AppendStreamEvents([
            AgUiMessageBuilderUnitTests.CreateEvent(1, StreamEventKind.TextStart, messageId: "other", messageRole: StreamMessageRole.Assistant),
            AgUiMessageBuilderUnitTests.CreateEvent(2, StreamEventKind.TextContent, messageId: "other", textDelta: "not mine"),
        ]);

        ContextObject graderContext = [];
        await InvokeAddAgUiOutput(repository, CreateEvalConfig(includeAgUiOutput: true), runId, graderContext);

        var message = RequireSingleMessage(graderContext, DefaultPath);
        Assert.Equal("Hello there", message.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Existing_grader_context_values_are_preserved()
    {
        var runId = Guid.NewGuid();
        var repository = CreateRepositoryWithRun(runId);
        ContextObject graderContext = [];
        graderContext.Set("expected.answer", "42");

        await InvokeAddAgUiOutput(repository, CreateEvalConfig(includeAgUiOutput: true), runId, graderContext);

        Assert.True(graderContext.TryGet<string>("expected.answer", out var answer));
        Assert.Equal("42", answer);
        Assert.Equal(1, MessageCount(graderContext, DefaultPath));
    }

    private static TestRepositoryService CreateRepositoryWithRun(Guid runId)
    {
        var repository = new TestRepositoryService();
        repository
            .AppendStreamEvents([
                AgUiMessageBuilderUnitTests.CreateEvent(1, StreamEventKind.TextStart, messageId: "m1", messageRole: StreamMessageRole.Assistant, runId: runId),
                AgUiMessageBuilderUnitTests.CreateEvent(2, StreamEventKind.TextContent, messageId: "m1", textDelta: "Hello", runId: runId),
                AgUiMessageBuilderUnitTests.CreateEvent(3, StreamEventKind.TextContent, messageId: "m1", textDelta: " there", runId: runId),
            ])
            .GetAwaiter()
            .GetResult();

        return repository;
    }

    private static JsonElement RequireMessages(ContextObject graderContext, string path)
    {
        Assert.True(graderContext.TryGet<string>(path, out var json));
        Assert.NotNull(json);

        var messages = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(JsonValueKind.Array, messages.ValueKind);
        return messages;
    }

    private static JsonElement RequireSingleMessage(ContextObject graderContext, string path)
    {
        var messages = RequireMessages(graderContext, path);
        Assert.Equal(1, messages.GetArrayLength());
        return messages[0];
    }

    private static int MessageCount(ContextObject graderContext, string path)
    {
        return RequireMessages(graderContext, path).GetArrayLength();
    }

    private static EvalConfig CreateEvalConfig(bool includeAgUiOutput, string? agUiOutputPath = null)
    {
        return new EvalConfig
        {
            EvalConfigId = Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            Name = "Eval",
            Description = "",
            MaxParallel = 1,
            IncludeAgUiOutput = includeAgUiOutput,
            AgUiOutputPath = agUiOutputPath,
        };
    }

    private static async Task InvokeAddAgUiOutput(IRepositoryService repository, EvalConfig evalConfig, Guid runId, ContextObject graderContext)
    {
        var method = typeof(EngineService).GetMethod(
            "AddAgUiOutputToGraderContext",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );
        Assert.NotNull(method);
        await (Task)method.Invoke(null, [repository, evalConfig, runId, graderContext])!;
    }
}
