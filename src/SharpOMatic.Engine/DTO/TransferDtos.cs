namespace SharpOMatic.Engine.DTO;

public class TransferExportRequest
{
    public bool IncludeSecrets { get; set; }
    public TransferSelection? Workflows { get; set; }
    public TransferSelection? Connectors { get; set; }
    public TransferSelection? Models { get; set; }
    public TransferSelection? Evaluations { get; set; }
    public TransferSelection? Assets { get; set; }
}

public class TransferSelection
{
    public bool All { get; set; }
    public List<Guid> Ids { get; set; } = [];
}

public class TransferEvaluationPackage
{
    public required EvalConfig EvalConfig { get; set; }
    public required List<EvalGrader> Graders { get; set; }
    public required List<EvalColumn> Columns { get; set; }
    public required List<EvalRow> Rows { get; set; }
    public required List<EvalData> Data { get; set; }
    public required List<EvalRun> Runs { get; set; }
    public required List<EvalRunRow> RunRows { get; set; }
    public required List<EvalRunRowGrader> RunRowGraders { get; set; }
    public required List<EvalRunGraderSummary> RunGraderSummaries { get; set; }
}

public class TransferImportResult
{
    public int WorkflowsImported { get; set; }
    public int ConnectorsImported { get; set; }
    public int ModelsImported { get; set; }
    public int EvaluationsImported { get; set; }
    public int EvaluationRunsImported { get; set; }
    public int AssetsImported { get; set; }
}

public class TransferEnvelope
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; }
    public string Type { get; set; } = "";
    public DateTime ExportedUtc { get; set; }
    public JsonElement Payload { get; set; }
}

public class TransferEnvelope<T>
{
    public int SchemaVersion { get; set; } = TransferEnvelope.CurrentSchemaVersion;
    public required string Type { get; set; }
    public DateTime ExportedUtc { get; set; }
    public required T Payload { get; set; }
}

public class TransferAssetPayload
{
    public required Guid AssetId { get; set; }
    public string? FolderName { get; set; }
    public required string Name { get; set; }
    public required string MediaType { get; set; }
    public required DateTime Created { get; set; }
    public required long SizeBytes { get; set; }
    public required string ContentBase64 { get; set; }
}

public class TransferImportFile
{
    public required string Name { get; set; }
    public required Stream Stream { get; set; }
}

public class TransferImportBatchResult
{
    public TransferImportResult Result { get; set; } = new();
    public int FilesProcessed { get; set; }
    public int FilesImported { get; set; }
    public int FilesFailed { get; set; }
}
