namespace SharpOMatic.Editor.DTO;

public sealed class ConversationTurnRequest
{
    public NodeResumeInput? ResumeInput { get; set; }
    public string? ResumeContextJson { get; set; }
    public ContextEntryListEntity? InputEntries { get; set; }
    public bool NeedsEditorEvents { get; set; }
    public string? StreamConversationId { get; set; }
}
