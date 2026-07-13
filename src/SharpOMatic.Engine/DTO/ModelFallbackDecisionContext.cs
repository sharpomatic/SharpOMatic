namespace SharpOMatic.Engine.DTO;

public sealed record ModelFallbackTarget(
    Guid ModelId,
    string? ModelName,
    string ModelConfigId,
    Guid ConnectorId,
    string? ConnectorName,
    string ConnectorConfigId,
    IReadOnlyDictionary<string, string?> ParameterValues
);

public sealed record ModelFallbackFailure(
    ModelFallbackFailureCategory Category,
    int? StatusCode,
    TimeSpan? RetryAfter,
    bool IsTransient
);

public sealed record ModelFallbackDecisionContext(
    Guid RunId,
    Guid WorkflowId,
    string? ConversationId,
    Guid NodeId,
    string NodeTitle,
    int FailedAttemptIndex,
    int ConfiguredModelCount,
    ModelFallbackTarget FailedModel,
    ModelFallbackTarget NextModel,
    Exception Exception,
    ModelFallbackFailure Failure,
    bool ResponseStarted,
    bool ToolInvocationStarted,
    bool DefaultDecision,
    string DefaultDecisionReason
);
