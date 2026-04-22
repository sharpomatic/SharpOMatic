namespace SharpOMatic.Engine.Enumerations;

public enum StreamEventKind
{
    TextStart = 0,
    TextContent = 1,
    TextEnd = 2,
    ReasoningStart = 3,
    ReasoningMessageStart = 4,
    ReasoningMessageContent = 5,
    ReasoningMessageEnd = 6,
    ReasoningEnd = 7,
    ToolCallStart = 8,
    ToolCallArgs = 9,
    ToolCallEnd = 10,
    ToolCallResult = 11,
    ActivitySnapshot = 12,
    ActivityDelta = 13,
    StepStart = 14,
    StepEnd = 15,
    StateSnapshot = 16,
    StateDelta = 17,
    Custom = 18,
}
