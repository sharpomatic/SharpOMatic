namespace SharpOMatic.Engine.Repository;

[Index(nameof(Created))]
[Index(nameof(WorkflowId), nameof(Created))]
[Index(nameof(ConnectorId), nameof(Created))]
[Index(nameof(ModelId), nameof(Created))]
[Index(nameof(ConversationId), nameof(Created))]
[Index(nameof(Succeeded), nameof(Created))]
public class ModelCallMetric
{
    [Key]
    public required Guid Id { get; set; }
    public required DateTime Created { get; set; }
    public long? Duration { get; set; }
    public required bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorType { get; set; }
    public required Guid WorkflowId { get; set; }
    public required string WorkflowName { get; set; }
    public required Guid RunId { get; set; }
    [MaxLength(256)]
    public string? ConversationId { get; set; }
    public required Guid NodeEntityId { get; set; }
    public required string NodeTitle { get; set; }
    public Guid? ConnectorId { get; set; }
    public string? ConnectorName { get; set; }
    public string? ConnectorConfigId { get; set; }
    public string? ConnectorConfigName { get; set; }
    public Guid? ModelId { get; set; }
    public string? ModelName { get; set; }
    public string? ModelConfigId { get; set; }
    public string? ModelConfigName { get; set; }
    public string? ProviderModelName { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
    public decimal? InputCost { get; set; }
    public decimal? OutputCost { get; set; }
    public decimal? TotalCost { get; set; }
}
