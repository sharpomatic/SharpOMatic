namespace SharpOMatic.AGUI.DTO;

public sealed class AgUiHistoryRequest
{
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; set; }

    [JsonPropertyName("workflowId")]
    public string? WorkflowId { get; set; }

    [JsonPropertyName("workflowName")]
    public string? WorkflowName { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
