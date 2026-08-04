namespace SharpOMatic.Tests.Workflows;

public sealed class ChatHistoryReplayHelperUnitTests
{
    [Fact]
    public void Orphaned_tool_call_is_dropped_after_model_call_exit_removes_its_result()
    {
        // Represents the state of result.Responses after RemoveModelCallExitToolResults has run:
        // the sentinel FunctionResultContent for "needs_input" was removed, leaving its
        // FunctionCallContent with no matching result — the orphaned case.
        var messages = new List<ChatMessage>
        {
            new(
                ChatRole.Assistant,
                [
                    new TextContent("Let me look that up."),
                    new FunctionCallContent("call-1", "lookup_weather", new Dictionary<string, object?> { ["city"] = "Sydney" }),
                ]
            ),
            new(
                ChatRole.Tool,
                [new FunctionResultContent("call-1", "Sunny")]
            ),
            new(
                ChatRole.Assistant,
                [
                    new TextContent("Now I need your input."),
                    new FunctionCallContent("call-2", "needs_input", new Dictionary<string, object?> { ["prompt"] = "confirm?" }),
                ]
            ),
            // No FunctionResultContent for call-2 — removed by RemoveModelCallExitToolResults
        };

        var output = ChatHistoryReplayHelper.CreatePortableOutputMessages(messages, dropToolCalls: false);
        var list = output.OfType<ChatMessage>().ToList();

        Assert.Equal(3, list.Count);

        // The matched pair survives with its roles and grouping intact.
        Assert.Equal(ChatRole.Assistant, list[0].Role);
        Assert.Equal(2, list[0].Contents.Count);
        Assert.Equal("Let me look that up.", Assert.IsType<TextContent>(list[0].Contents[0]).Text);
        var functionCallContent = Assert.IsType<FunctionCallContent>(list[0].Contents[1]);
        Assert.Equal("call-1", functionCallContent.CallId);
        Assert.Equal("lookup_weather", functionCallContent.Name);
        Assert.Equal("Sydney", Assert.IsType<string>(functionCallContent.Arguments!["city"]));

        Assert.Equal(ChatRole.Tool, list[1].Role);
        var functionResultContent = Assert.IsType<FunctionResultContent>(list[1].Contents.Single());
        Assert.Equal("call-1", functionResultContent.CallId);
        Assert.Equal("Sunny", Assert.IsType<string>(functionResultContent.Result));

        // The orphaned call-2 is dropped: replaying an unanswered tool call is invalid for every provider,
        // so only its surrounding assistant text remains.
        Assert.Equal(ChatRole.Assistant, list[2].Role);
        Assert.Equal("Now I need your input.", Assert.IsType<TextContent>(list[2].Contents.Single()).Text);
    }

    [Fact]
    public void Orphaned_tool_call_is_suppressed_when_drop_tool_calls_is_true()
    {
        var messages = new List<ChatMessage>
        {
            new(
                ChatRole.Assistant,
                [
                    new TextContent("Some text."),
                    new FunctionCallContent("call-1", "needs_input", new Dictionary<string, object?>()),
                ]
            ),
        };

        var output = ChatHistoryReplayHelper.CreatePortableOutputMessages(messages, dropToolCalls: true);
        var list = output.OfType<ChatMessage>().ToList();

        var single = Assert.Single(list);
        Assert.Equal("Some text.", Assert.IsType<TextContent>(single.Contents.Single()).Text);
    }

    [Fact]
    public void Matched_tool_calls_are_emitted_as_native_content()
    {
        var messages = new List<ChatMessage>
        {
            new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent("call-1", "get_time", new Dictionary<string, object?>()),
                    new FunctionResultContent("call-1", "Noon"),
                ]
            ),
        };

        var output = ChatHistoryReplayHelper.CreatePortableOutputMessages(messages, dropToolCalls: false);
        var list = output.OfType<ChatMessage>().ToList();

        var single = Assert.Single(list);
        Assert.Equal(2, single.Contents.Count);

        var functionCallContent = Assert.IsType<FunctionCallContent>(single.Contents[0]);
        Assert.Equal("call-1", functionCallContent.CallId);
        Assert.Equal("get_time", functionCallContent.Name);

        var functionResultContent = Assert.IsType<FunctionResultContent>(single.Contents[1]);
        Assert.Equal("call-1", functionResultContent.CallId);
        Assert.Equal("Noon", Assert.IsType<string>(functionResultContent.Result));
    }

    [Fact]
    public void Tool_result_without_a_matching_call_is_dropped()
    {
        var messages = new List<ChatMessage>
        {
            new(
                ChatRole.Assistant,
                [new TextContent("Answer.")]
            ),
            new(
                ChatRole.Tool,
                [new FunctionResultContent("call-missing", "Stale result")]
            ),
        };

        var output = ChatHistoryReplayHelper.CreatePortableOutputMessages(messages, dropToolCalls: false);
        var list = output.OfType<ChatMessage>().ToList();

        // A result with no call is rejected by providers for the same reason an unanswered call is, so the
        // whole tool message drops out rather than replaying half a pair.
        var single = Assert.Single(list);
        Assert.Equal(ChatRole.Assistant, single.Role);
        Assert.Equal("Answer.", Assert.IsType<TextContent>(single.Contents.Single()).Text);
    }
}
