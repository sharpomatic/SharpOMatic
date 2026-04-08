namespace SharpOMatic.Editor.DTO;

public sealed record class ConversationHistoryTurnResult(
    Run Run,
    List<Trace> Traces,
    List<Information> Informations,
    List<StreamEvent> StreamEvents,
    List<AssetSummary> Assets
);
