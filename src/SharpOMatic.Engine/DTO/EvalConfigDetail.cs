namespace SharpOMatic.Engine.DTO;

public class EvalConfigDetail
{
    public required EvalConfig EvalConfig { get; set; }
    public required List<EvalGrader> Graders { get; set; }
    public required List<EvalColumn> Columns { get; set; }
    public required List<EvalRow> Rows { get; set; }
    public required List<EvalData> Data { get; set; }
}
