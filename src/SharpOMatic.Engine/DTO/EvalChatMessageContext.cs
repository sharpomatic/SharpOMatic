namespace SharpOMatic.Engine.DTO;

public sealed record EvalChatMessageContext(
    Guid EvalConfigId,
    Guid EvalRunId,
    Guid EvalRunRowId,
    Guid EvalRowId,
    string RowName,
    int RowOrder,
    int ExecutionOrder,
    Guid RunId,
    Guid WorkflowId,
    string? ConversationId,
    ContextObject GraderContext
);
