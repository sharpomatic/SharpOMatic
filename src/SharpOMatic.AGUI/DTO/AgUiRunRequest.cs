namespace SharpOMatic.AGUI.DTO;

public sealed class AgUiRunRequest
{
    public string? ThreadId { get; set; }
    public string? RunId { get; set; }
    public string? ParentRunId { get; set; }
    public JsonElement? Messages { get; set; }
    public JsonElement? State { get; set; }
    public JsonElement? Tools { get; set; }
    public JsonElement? Context { get; set; }
    public JsonElement? ForwardedProps { get; set; }
}
