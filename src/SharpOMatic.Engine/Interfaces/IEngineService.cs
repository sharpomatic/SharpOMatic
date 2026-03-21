namespace SharpOMatic.Engine.Interfaces;

public interface IEngineService
{
    Task<Guid> GetWorkflowId(string workflowName);
    Task<Guid> CreateWorkflowRun(Guid workflowId, bool needsEditorEvents = false);
    Task<Run> StartWorkflowRunAndWait(Guid runId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null);
    Task StartWorkflowRunAndNotify(Guid runId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null);
    Guid CreateWorkflowRunSynchronously(Guid workflowId, bool needsEditorEvents = false);
    Run StartWorkflowRunSynchronously(Guid runId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null);
    Task<EvalRun> StartEvalRun(Guid evalConfigId, string? name = null, int? sampleCount = null);
}
