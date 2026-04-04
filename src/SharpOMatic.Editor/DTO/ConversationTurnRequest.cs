namespace SharpOMatic.Editor.DTO;

public sealed class ConversationTurnRequest
{
    public NodeResumeInput? ResumeInput { get; set; }
    public ContextEntryListEntity? InputEntries { get; set; }
    public bool NeedsEditorEvents { get; set; }
}
