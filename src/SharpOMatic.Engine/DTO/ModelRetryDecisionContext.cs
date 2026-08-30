namespace SharpOMatic.Engine.DTO;

public sealed record ModelRetryDecision(bool ShouldRetry, TimeSpan Delay);

public sealed record ModelRetryDecisionContext(
    Guid RunId,
    Guid WorkflowId,
    string? ConversationId,
    Guid NodeId,
    string NodeTitle,
    int AttemptIndex,
    int ConfiguredModelCount,
    ModelFallbackTarget FailedModel,
    Exception Exception,
    ModelFallbackFailure Failure,
    int TryNumber,
    int MaxTries,
    TimeSpan Elapsed,
    bool ResponseStarted,
    bool ToolInvocationStarted,
    ModelRetryDecision DefaultDecision,
    string DefaultDecisionReason
);
