namespace SharpOMatic.Engine.Interfaces;

public interface IEngineNotification
{
    public Task RunCompleted(Guid runId, Guid workflowId, Guid? conversationId, RunStatus runStatus, string? outputContext, string? error);

    public Task EvalRunCompleted(Guid evalRunId, EvalRunStatus runStatus, string? error);

    public void ConnectionOverride(Guid runId, Guid workflowId, Guid? conversationId, string connectorId, AuthenticationModeConfig authenticationModel, Dictionary<string, string?> parameters);
}
