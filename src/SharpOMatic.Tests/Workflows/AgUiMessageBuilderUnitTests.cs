namespace SharpOMatic.Tests.Workflows;

public sealed class AgUiMessageBuilderUnitTests
{
    [Fact]
    public void Text_deltas_are_folded_into_a_single_assistant_message()
    {
        var events = new List<StreamEvent>
        {
            CreateEvent(1, StreamEventKind.TextStart, messageId: "m1", messageRole: StreamMessageRole.Assistant),
            CreateEvent(2, StreamEventKind.TextContent, messageId: "m1", textDelta: "Hello"),
            CreateEvent(3, StreamEventKind.TextContent, messageId: "m1", textDelta: " there"),
            CreateEvent(4, StreamEventKind.TextEnd, messageId: "m1"),
        };

        var message = Assert.Single(AgUiMessageBuilder.BuildMessages(events));

        Assert.Equal("m1", message["id"]);
        Assert.Equal("assistant", message["role"]);
        Assert.Equal("Hello there", message["content"]);
    }

    [Fact]
    public void Messages_are_assembled_in_sequence_order_regardless_of_input_order()
    {
        var events = new List<StreamEvent>
        {
            CreateEvent(3, StreamEventKind.TextContent, messageId: "m1", textDelta: "world"),
            CreateEvent(1, StreamEventKind.TextStart, messageId: "m1", messageRole: StreamMessageRole.Assistant),
            CreateEvent(2, StreamEventKind.TextContent, messageId: "m1", textDelta: "hello "),
        };

        var message = Assert.Single(AgUiMessageBuilder.BuildMessages(events));

        Assert.Equal("hello world", message["content"]);
    }

    [Fact]
    public void Tool_call_arguments_are_concatenated_and_the_result_becomes_a_tool_message()
    {
        var events = new List<StreamEvent>
        {
            CreateEvent(1, StreamEventKind.ToolCallStart, messageId: "call-1", toolCallId: "call-1", textDelta: "GetWeather"),
            CreateEvent(2, StreamEventKind.ToolCallArgs, messageId: "call-1", toolCallId: "call-1", textDelta: "{\"city\":"),
            CreateEvent(3, StreamEventKind.ToolCallArgs, messageId: "call-1", toolCallId: "call-1", textDelta: "\"Perth\"}"),
            CreateEvent(4, StreamEventKind.ToolCallEnd, messageId: "call-1", toolCallId: "call-1"),
            CreateEvent(5, StreamEventKind.ToolCallResult, messageId: "m1", toolCallId: "call-1", textDelta: "Sunny"),
        };

        var messages = AgUiMessageBuilder.BuildMessages(events);

        Assert.Equal(2, messages.Count);

        var assistant = messages[0];
        Assert.Equal("assistant", assistant["role"]);
        var toolCall = Assert.Single(Assert.IsType<List<Dictionary<string, object?>>>(assistant["toolCalls"]));
        Assert.Equal("call-1", toolCall["id"]);
        Assert.Equal("function", toolCall["type"]);

        var function = Assert.IsType<Dictionary<string, object?>>(toolCall["function"]);
        Assert.Equal("GetWeather", function["name"]);
        Assert.Equal("{\"city\":\"Perth\"}", function["arguments"]);

        var toolResult = messages[1];
        Assert.Equal("tool", toolResult["role"]);
        Assert.Equal("call-1", toolResult["toolCallId"]);
        Assert.Equal("Sunny", toolResult["content"]);
    }

    [Fact]
    public void Reasoning_messages_are_assembled_under_a_prefixed_id()
    {
        var events = new List<StreamEvent>
        {
            CreateEvent(1, StreamEventKind.ReasoningMessageStart, messageId: "m1"),
            CreateEvent(2, StreamEventKind.ReasoningMessageContent, messageId: "m1", textDelta: "thinking"),
        };

        var message = Assert.Single(AgUiMessageBuilder.BuildMessages(events));

        Assert.Equal("reason:m1", message["id"]);
        Assert.Equal("reasoning", message["role"]);
        Assert.Equal("thinking", message["content"]);
    }

    [Fact]
    public void Activity_snapshots_are_patched_by_later_activity_deltas()
    {
        var events = new List<StreamEvent>
        {
            CreateEvent(1, StreamEventKind.ActivitySnapshot, messageId: "a1", activityType: "progress", textDelta: "{\"step\":1}"),
            CreateEvent(2, StreamEventKind.ActivityDelta, messageId: "a1", activityType: "progress", textDelta: "[{\"op\":\"replace\",\"path\":\"/step\",\"value\":2}]"),
        };

        var message = Assert.Single(AgUiMessageBuilder.BuildMessages(events));

        Assert.Equal("activity:a1", message["id"]);
        Assert.Equal("activity", message["role"]);
        Assert.Equal("progress", message["activityType"]);

        var content = Assert.IsType<JsonObject>(message["content"]);
        Assert.Equal(2, content["step"]!.GetValue<int>());
    }

    [Fact]
    public void Events_that_do_not_form_messages_are_ignored()
    {
        var events = new List<StreamEvent>
        {
            CreateEvent(1, StreamEventKind.StepStart, textDelta: "step-1"),
            CreateEvent(2, StreamEventKind.StateSnapshot, textDelta: "{\"a\":1}"),
            CreateEvent(3, StreamEventKind.StepEnd, textDelta: "step-1"),
        };

        Assert.Empty(AgUiMessageBuilder.BuildMessages(events));
    }

    internal static StreamEvent CreateEvent(
        int sequenceNumber,
        StreamEventKind eventKind,
        string? messageId = null,
        StreamMessageRole? messageRole = null,
        string? textDelta = null,
        string? toolCallId = null,
        string? activityType = null,
        Guid? runId = null,
        bool hideFromReply = false
    )
    {
        return new StreamEvent
        {
            StreamEventId = Guid.NewGuid(),
            RunId = runId ?? Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            SequenceNumber = sequenceNumber,
            Created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(sequenceNumber),
            EventKind = eventKind,
            MessageId = messageId,
            MessageRole = messageRole,
            TextDelta = textDelta,
            ToolCallId = toolCallId,
            ActivityType = activityType,
            HideFromReply = hideFromReply,
        };
    }
}
