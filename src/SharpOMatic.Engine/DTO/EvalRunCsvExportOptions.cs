namespace SharpOMatic.Engine.DTO;

public class EvalRunCsvExportOptions
{
    public List<Guid> ColumnIds { get; set; } = [];
    public List<EvalRunExportField> Fields { get; set; } = [];
    public List<string> GraderPaths { get; set; } = [];
}
