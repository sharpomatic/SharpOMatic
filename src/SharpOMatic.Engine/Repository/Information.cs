namespace SharpOMatic.Engine.Repository;

[Index(nameof(TraceId), nameof(Created))]
public class Information
{
    [Key]
    public required Guid InformationId { get; set; }
    public required Guid TraceId { get; set; }
    public required Guid RunId { get; set; }
    public required DateTime Created { get; set; }
    public required InformationType InformationType { get; set; }
    public required string Text { get; set; }
    public string? Data { get; set; }
}
