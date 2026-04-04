namespace SharpOMatic.Editor.DTO;

public sealed record class ConversationHistoryResult(
    Guid ConversationId,
    Guid WorkflowId,
    Run? LatestRun,
    List<ConversationHistoryTurnResult> Turns,
    List<AssetSummary> ConversationAssets
);
