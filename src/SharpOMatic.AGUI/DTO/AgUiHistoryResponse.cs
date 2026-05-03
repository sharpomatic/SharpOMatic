namespace SharpOMatic.AGUI.DTO;

public sealed class AgUiHistoryResponse
{
    [JsonPropertyName("messages")]
    public required List<Dictionary<string, object?>> Messages { get; init; }

    [JsonPropertyName("state")]
    public JsonNode? State { get; init; }

    [JsonPropertyName("pendingFrontendTools")]
    public required List<AgUiPendingFrontendTool> PendingFrontendTools { get; init; }
}

public sealed class AgUiPendingFrontendTool
{
    [JsonPropertyName("toolCallId")]
    public required string ToolCallId { get; init; }

    [JsonPropertyName("toolName")]
    public required string ToolName { get; init; }

    [JsonPropertyName("argumentsJson")]
    public required string ArgumentsJson { get; init; }

    [JsonPropertyName("assistantMessageId")]
    public required string AssistantMessageId { get; init; }
}
