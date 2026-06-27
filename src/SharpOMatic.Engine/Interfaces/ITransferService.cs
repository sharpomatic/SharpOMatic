namespace SharpOMatic.Engine.Interfaces;

public interface ITransferService
{
    Task ExportAsync(TransferExportRequest request, Stream output, CancellationToken cancellationToken = default);
    Task<TransferImportBatchResult> ImportZipAsync(Stream input, CancellationToken cancellationToken = default);
    Task<TransferImportBatchResult> ImportFilesAsync(IEnumerable<TransferImportFile> files, CancellationToken cancellationToken = default);
    Task<TransferImportResult> ImportJsonAsync(Stream input, CancellationToken cancellationToken = default);
    Task<EvalRowsCsvImportResult> ImportEvalRowsCsvAsync(Guid evalConfigId, Stream input, string fileName, CancellationToken cancellationToken = default);
    Task<Stream> ExportEvalRowsCsvAsync(Guid evalConfigId, CancellationToken cancellationToken = default);
}
