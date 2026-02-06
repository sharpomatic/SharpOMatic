namespace SharpOMatic.Engine.Interfaces;

public interface ITransferService
{
    Task ExportAsync(TransferExportRequest request, Stream output, CancellationToken cancellationToken = default);
    Task<TransferImportResult> ImportAsync(Stream input, CancellationToken cancellationToken = default);
}
