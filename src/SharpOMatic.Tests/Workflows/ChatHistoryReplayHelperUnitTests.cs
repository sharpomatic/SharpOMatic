namespace SharpOMatic.Tests.Workflows;

public sealed class ChatHistoryReplayHelperUnitTests
{
    [Fact]
    public void Orphaned_tool_call_is_emitted_after_graceful_stop_removes_its_result()
    {
        // Represents the state of result.Responses after RemoveGracefulStopToolResults has run:
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
            // No FunctionResultContent for call-2 — removed by RemoveGracefulStopToolResults
        };

        var output = ChatHistoryReplayHelper.CreatePortableOutputMessages(messages, dropToolCalls: false);
        var list = output.OfType<ChatMessage>().ToList();

        Assert.Equal(4, list.Count);

        Assert.Equal(ChatRole.Assistant, list[0].Role);
        Assert.Equal("Let me look that up.", Assert.IsType<TextContent>(list[0].Contents.Single()).Text);

        Assert.Equal(ChatRole.Assistant, list[1].Role);
        Assert.Equal(
            "Invoked Tool Call, Name = lookup_weather, Arguments = {\"city\":\"Sydney\"}, Result = Sunny",
            Assert.IsType<TextContent>(list[1].Contents.Single()).Text
        );

        Assert.Equal(ChatRole.Assistant, list[2].Role);
        Assert.Equal("Now I need your input.", Assert.IsType<TextContent>(list[2].Contents.Single()).Text);

        // The orphaned call-2 must appear as a tool call message without a result line.
        Assert.Equal(ChatRole.Assistant, list[3].Role);
        Assert.Equal(
            "Invoked Tool Call, Name = needs_input, Arguments = {\"prompt\":\"confirm?\"}",
            Assert.IsType<TextContent>(list[3].Contents.Single()).Text
        );
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
    public void Completed_tool_calls_are_not_affected_by_the_orphan_fix()
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
        Assert.Equal(
            "Invoked Tool Call, Name = get_time, Result = Noon",
            Assert.IsType<TextContent>(single.Contents.Single()).Text
        );
    }
}
