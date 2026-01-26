namespace SharpOMatic.Engine.Repository;

public class EvalRow
{
    [Key]
    public required Guid EvalRowId { get; set; }
    public required Guid EvalConfigId { get; set; }
    public required int Order { get; set; }
}
