#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Interfaces;

public interface IEngineNotification
{
    public Task RunCompleted(Guid runId, Guid workflowId, string? conversationId, RunStatus runStatus, string? outputContext, string? error) => Task.CompletedTask;

    public Task EvalRunCompleted(Guid evalRunId, EvalRunStatus runStatus, string? error) => Task.CompletedTask;

    public void ConnectionOverride(Guid runId, Guid workflowId, string? conversationId, string connectorId, AuthenticationModeConfig authenticationModel, Dictionary<string, string?> parameters) { }

    public (ResponsesClient client, string modelName)? OpenAIOverride(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields
    ) => null;

    public (ResponsesClient client, string modelName)? AzureOpenAIOverride(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields
    ) => null;

    public IChatClient? GoogleGenAIOverride(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields
    ) => null;
}
