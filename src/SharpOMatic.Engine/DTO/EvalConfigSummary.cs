namespace SharpOMatic.Engine.DTO;

public class EvalConfigSummary
{
    public required Guid EvalConfigId { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Modified { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}
