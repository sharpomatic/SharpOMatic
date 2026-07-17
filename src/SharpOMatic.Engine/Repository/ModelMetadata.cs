namespace SharpOMatic.Engine.Repository;

public class ModelMetadata
{
    [Key]
    public required Guid ModelId { get; set; }
    public required int Version { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Modified { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Config { get; set; }
}
