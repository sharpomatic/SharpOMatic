namespace SharpOMatic.Engine.Interfaces;

public interface ITransferService
{
    Task ExportAsync(TransferExportRequest request, Stream output, CancellationToken cancellationToken = default);
    Task<TransferImportBatchResult> ImportZipAsync(Stream input, CancellationToken cancellationToken = default);
    Task<TransferImportBatchResult> ImportFilesAsync(IEnumerable<TransferImportFile> files, CancellationToken cancellationToken = default);
    Task<TransferImportResult> ImportJsonAsync(Stream input, CancellationToken cancellationToken = default);
}
