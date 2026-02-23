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

public class TransferManifest
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTime CreatedUtc { get; set; }
    public bool IncludeSecrets { get; set; }
    public TransferCounts Counts { get; set; } = new();
    public List<TransferFolderEntry> Folders { get; set; } = [];
    public List<TransferAssetEntry> Assets { get; set; } = [];
}

public class TransferCounts
{
    public int Workflows { get; set; }
    public int Connectors { get; set; }
    public int Models { get; set; }
    public int Evaluations { get; set; }
    public int Folders { get; set; }
    public int Assets { get; set; }
}

public class TransferFolderEntry
{
    public Guid FolderId { get; set; }
    public string Name { get; set; } = "";
    public DateTime Created { get; set; }
}

public class TransferAssetEntry
{
    public Guid AssetId { get; set; }
    public Guid? FolderId { get; set; }
    public string Name { get; set; } = "";
    public string MediaType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime Created { get; set; }
}

public class TransferImportResult
{
    public int WorkflowsImported { get; set; }
    public int ConnectorsImported { get; set; }
    public int ModelsImported { get; set; }
    public int EvaluationsImported { get; set; }
    public int AssetsImported { get; set; }
}
