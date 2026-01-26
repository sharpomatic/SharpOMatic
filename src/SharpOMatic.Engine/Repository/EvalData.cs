namespace SharpOMatic.Engine.Repository;

public class EvalData
{
    [Key]
    public required Guid EvalDataId { get; set; }
    public required Guid EvalRowId { get; set; }
    public required Guid EvalColumnId { get; set; }
    public string? StringValue { get; set; }
    public int? IntValue { get; set; }
    public double? DoubleValue { get; set; }
    public bool? BoolValue { get; set; }
}
