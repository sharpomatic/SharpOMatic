namespace SharpOMatic.Engine.Interfaces;

public interface IEngineService
{
    Task<Guid> GetWorkflowId(string workflowName);
    Task<Run> StartWorkflowRunAndWait(Guid workflowId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false);
    Task<Guid> StartWorkflowRunAndNotify(Guid workflowId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false);
    Run StartWorkflowRunSynchronously(Guid workflowId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false);
    Task<EvalRun> StartEvalRun(Guid evalConfigId, string? name = null, int? sampleCount = null);
    Task<Run> StartOrResumeConversationAndWait(Guid workflowId, Guid conversationId, NodeResumeInput? resumeInput = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false, string? streamConversationId = null);
    Task<Guid> StartOrResumeConversationAndNotify(Guid workflowId, Guid conversationId, NodeResumeInput? resumeInput = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false, string? streamConversationId = null);
    Run StartOrResumeConversationSynchronously(Guid workflowId, Guid conversationId, NodeResumeInput? resumeInput = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false, string? streamConversationId = null);
}
