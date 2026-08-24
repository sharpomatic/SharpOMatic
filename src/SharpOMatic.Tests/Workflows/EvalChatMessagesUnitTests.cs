namespace SharpOMatic.Tests.Workflows;

public sealed class EvalChatMessagesUnitTests
{
    private const string DefaultPath = "chat.messages";

    [Fact]
    public async Task Chat_messages_are_added_at_the_default_path_when_no_path_is_configured()
    {
        ContextObject graderContext = [];
        var notification = new StoredChatMessagesNotification([new ChatMessage(ChatRole.User, "How much is it?")]);

        await InvokeAddChatMessages([notification], CreateEvalConfig(includeChatMessages: true), graderContext);

        var message = RequireSingleMessage(graderContext, DefaultPath);
        Assert.Equal("user", message.GetProperty("role").GetString());
        Assert.Equal("How much is it?", RequireText(message));
    }

    [Fact]
    public async Task Chat_messages_are_added_at_the_configured_path()
    {
        ContextObject graderContext = [];
        var notification = new StoredChatMessagesNotification([new ChatMessage(ChatRole.User, "Hello")]);

        await InvokeAddChatMessages([notification], CreateEvalConfig(includeChatMessages: true, chatMessagesPath: "stored.chat"), graderContext);

        Assert.Equal(1, MessageCount(graderContext, "stored.chat"));
        Assert.False(graderContext.TryGet<object?>(DefaultPath, out _));
    }

    [Fact]
    public async Task Configured_path_is_trimmed_before_use()
    {
        ContextObject graderContext = [];
        var notification = new StoredChatMessagesNotification([new ChatMessage(ChatRole.User, "Hello")]);

        await InvokeAddChatMessages([notification], CreateEvalConfig(includeChatMessages: true, chatMessagesPath: "  stored.chat  "), graderContext);

        Assert.Equal(1, MessageCount(graderContext, "stored.chat"));
    }

    [Fact]
    public async Task Nothing_is_added_when_the_evaluation_does_not_ask_for_chat_messages()
    {
        ContextObject graderContext = [];
        var notification = new StoredChatMessagesNotification([new ChatMessage(ChatRole.User, "Hello")]);

        await InvokeAddChatMessages([notification], CreateEvalConfig(includeChatMessages: false), graderContext);

        Assert.Empty(graderContext);
        Assert.False(notification.WasCalled);
    }

    [Fact]
    public async Task Nothing_is_added_when_no_host_implementation_claims_the_lookup()
    {
        ContextObject graderContext = [];

        await InvokeAddChatMessages([new StoredChatMessagesNotification(null)], CreateEvalConfig(includeChatMessages: true), graderContext);

        Assert.Empty(graderContext);
    }

    [Fact]
    public async Task Nothing_is_added_when_the_host_does_not_implement_the_lookup()
    {
        ContextObject graderContext = [];

        // A host that registers a notification for other reasons inherits the default implementation, which must be
        // a no-op rather than a failure or an empty list.
        await InvokeAddChatMessages([new UnrelatedNotification()], CreateEvalConfig(includeChatMessages: true), graderContext);

        Assert.Empty(graderContext);
        Assert.False(graderContext.TryGet<object?>(DefaultPath, out _));
    }

    [Fact]
    public async Task Nothing_is_added_when_there_are_no_host_implementations_at_all()
    {
        ContextObject graderContext = [];

        await InvokeAddChatMessages([], CreateEvalConfig(includeChatMessages: true), graderContext);

        Assert.Empty(graderContext);
    }

    [Fact]
    public async Task An_empty_list_is_written_so_a_grader_can_tell_it_apart_from_no_lookup()
    {
        ContextObject graderContext = [];

        await InvokeAddChatMessages([new StoredChatMessagesNotification([])], CreateEvalConfig(includeChatMessages: true), graderContext);

        Assert.Equal(0, MessageCount(graderContext, DefaultPath));
    }

    [Fact]
    public async Task The_first_non_null_result_wins()
    {
        ContextObject graderContext = [];
        var skipped = new StoredChatMessagesNotification(null);
        var claimed = new StoredChatMessagesNotification([new ChatMessage(ChatRole.Assistant, "claimed")]);
        var ignored = new StoredChatMessagesNotification([new ChatMessage(ChatRole.Assistant, "ignored")]);

        await InvokeAddChatMessages([skipped, claimed, ignored], CreateEvalConfig(includeChatMessages: true), graderContext);

        var message = RequireSingleMessage(graderContext, DefaultPath);
        Assert.Equal("claimed", RequireText(message));
        Assert.True(skipped.WasCalled);
        Assert.True(claimed.WasCalled);
        Assert.False(ignored.WasCalled);
    }

    [Fact]
    public async Task A_failing_host_implementation_fails_the_row_with_the_row_name()
    {
        ContextObject graderContext = [];

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            InvokeAddChatMessages([new ThrowingChatMessagesNotification()], CreateEvalConfig(includeChatMessages: true), graderContext)
        );

        Assert.Contains("Only row", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task The_lookup_receives_the_row_identity_and_the_grader_context()
    {
        ContextObject graderContext = [];
        graderContext.Set("input.conversationId", "conversation-7");
        var notification = new StoredChatMessagesNotification([]);

        await InvokeAddChatMessages([notification], CreateEvalConfig(includeChatMessages: true), graderContext);

        Assert.NotNull(notification.ReceivedContext);
        Assert.Equal("Only row", notification.ReceivedContext.RowName);
        Assert.True(notification.ReceivedContext.GraderContext.TryGet<string>("input.conversationId", out var conversationId));
        Assert.Equal("conversation-7", conversationId);
    }

    [Fact]
    public async Task Messages_are_cloned_so_later_host_mutations_do_not_reach_the_grader()
    {
        ContextObject graderContext = [];
        var hostMessage = new ChatMessage(ChatRole.User, "original");
        var notification = new StoredChatMessagesNotification([hostMessage]);

        await InvokeAddChatMessages([notification], CreateEvalConfig(includeChatMessages: true), graderContext);
        hostMessage.Contents.Clear();
        hostMessage.Contents.Add(new TextContent("mutated"));

        var message = RequireSingleMessage(graderContext, DefaultPath);
        Assert.Equal("original", RequireText(message));
    }

    [Fact]
    public async Task Existing_grader_context_values_are_preserved()
    {
        ContextObject graderContext = [];
        graderContext.Set("expected.answer", "42");

        await InvokeAddChatMessages([new StoredChatMessagesNotification([new ChatMessage(ChatRole.User, "Hello")])], CreateEvalConfig(includeChatMessages: true), graderContext);

        Assert.True(graderContext.TryGet<string>("expected.answer", out var answer));
        Assert.Equal("42", answer);
        Assert.Equal(1, MessageCount(graderContext, DefaultPath));
    }

    [Fact]
    public void The_rendered_json_is_plain_and_omits_nulls()
    {
        var json = PromptJsonHelper.Serialize(ChatHistoryReplayHelper.CreatePortableStoredMessages([new ChatMessage(ChatRole.User, "Ask")]));

        Assert.Equal("""[{"role":"user","contents":[{"$type":"text","text":"Ask"}]}]""", json);
    }

    [Fact]
    public void The_persistence_type_envelope_is_absent_from_the_rendered_json()
    {
        var json = PromptJsonHelper.Serialize(ChatHistoryReplayHelper.CreatePortableStoredMessages([new ChatMessage(ChatRole.User, "Ask")]));

        Assert.DoesNotContain("\"$type\":\"ChatMessage\"", json);
        Assert.DoesNotContain("\"value\":", json);
    }

    [Fact]
    public async Task Template_markers_in_a_stored_message_are_neutralized()
    {
        ContextObject graderContext = [];
        graderContext.Set("expected.answer", "42");
        var risky = new ChatMessage(ChatRole.User, "compare {{expected.answer}} and <<secret>> please");

        await InvokeAddChatMessages([new StoredChatMessagesNotification([risky])], CreateEvalConfig(includeChatMessages: true), graderContext);

        Assert.True(graderContext.TryGet<string>(DefaultPath, out var json));
        Assert.NotNull(json);
        Assert.DoesNotContain("{{", json);
        Assert.DoesNotContain("<<", json);

        // The escapes are ordinary JSON, so anything that parses the value still reads the original text.
        Assert.Equal("compare {{expected.answer}} and <<secret>> please", RequireText(RequireSingleMessage(graderContext, DefaultPath)));
    }

    [Fact]
    public async Task A_stored_message_cannot_inject_a_template_into_the_grader_prompt()
    {
        ContextObject graderContext = [];
        graderContext.Set("expected.answer", "42");
        var risky = new ChatMessage(ChatRole.User, "the answer is {{expected.answer}}");

        await InvokeAddChatMessages([new StoredChatMessagesNotification([risky])], CreateEvalConfig(includeChatMessages: true), graderContext);

        using var provider = WorkflowRunner.BuildProvider(_ => { });
        var rendered = await ContextHelpers.SubstituteValuesAsync(
            "{{" + DefaultPath + "}}",
            graderContext,
            new TestRepositoryService(),
            provider.GetRequiredService<IAssetStore>(),
            Guid.NewGuid(),
            null
        );

        // The expected answer must not leak into the prompt through the stored conversation.
        Assert.DoesNotContain("the answer is 42", rendered);
        Assert.Contains("expected.answer", rendered);
    }

    [Fact]
    public async Task Tool_calls_keep_their_structure_in_the_rendered_json()
    {
        ContextObject graderContext = [];
        var toolCall = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "GetWeather", new Dictionary<string, object?> { ["city"] = "Perth" })]);

        await InvokeAddChatMessages([new StoredChatMessagesNotification([toolCall])], CreateEvalConfig(includeChatMessages: true), graderContext);

        var content = RequireSingleMessage(graderContext, DefaultPath).GetProperty("contents")[0];
        Assert.Equal("functionCall", content.GetProperty("$type").GetString());
        Assert.Equal("GetWeather", content.GetProperty("name").GetString());
        Assert.Equal("Perth", content.GetProperty("arguments").GetProperty("city").GetString());
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

    private static string RequireText(JsonElement message)
    {
        var contents = message.GetProperty("contents");
        Assert.Equal(JsonValueKind.Array, contents.ValueKind);
        return contents[0].GetProperty("text").GetString() ?? string.Empty;
    }

    private static EvalConfig CreateEvalConfig(bool includeChatMessages, string? chatMessagesPath = null)
    {
        return new EvalConfig
        {
            EvalConfigId = Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            Name = "Eval",
            Description = "",
            MaxParallel = 1,
            IncludeChatMessages = includeChatMessages,
            ChatMessagesPath = chatMessagesPath,
        };
    }

    private static async Task InvokeAddChatMessages(IEnumerable<IEngineNotification> notifications, EvalConfig evalConfig, ContextObject graderContext)
    {
        var chatMessageContext = new EvalChatMessageContext(
            evalConfig.EvalConfigId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Only row",
            RowOrder: 1,
            ExecutionOrder: 0,
            Guid.NewGuid(),
            evalConfig.WorkflowId ?? Guid.Empty,
            null,
            graderContext
        );

        var method = typeof(EngineService).GetMethod("AddChatMessagesToGraderContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        try
        {
            await (Task)method.Invoke(null, [notifications, evalConfig, chatMessageContext, graderContext])!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private sealed class StoredChatMessagesNotification(IList<ChatMessage>? messages) : IEngineNotification
    {
        public bool WasCalled { get; private set; }
        public EvalChatMessageContext? ReceivedContext { get; private set; }

        public ValueTask<IList<ChatMessage>?> EvalChatMessages(EvalChatMessageContext context, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ReceivedContext = context;
            return ValueTask.FromResult(messages);
        }
    }

    private sealed class UnrelatedNotification : IEngineNotification { }

    private sealed class ThrowingChatMessagesNotification : IEngineNotification
    {
        public ValueTask<IList<ChatMessage>?> EvalChatMessages(EvalChatMessageContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("conversation store unavailable");
        }
    }
}
