namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransferController(ITransferService transferService) : ControllerBase
{
    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] TransferExportRequest request)
    {
        var bodyControl = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (bodyControl is not null)
            bodyControl.AllowSynchronousIO = true;

        var fileName = $"sharpomatic-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        Response.ContentType = "application/zip";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";

        await transferService.ExportAsync(request, Response.Body, HttpContext.RequestAborted);
        return new EmptyResult();
    }

    [HttpPost("import/zip")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportZip([FromForm] TransferImportRequest request)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest("File is required.");

        await using var stream = request.File.OpenReadStream();
        var result = await transferService.ImportZipAsync(stream, HttpContext.RequestAborted);
        return ImportResult(result);
    }

    [HttpPost("import/files")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportFiles([FromForm] TransferImportFilesRequest request)
    {
        var uploadedFiles = request.Files.Where(file => file.Length > 0).ToList();
        if (uploadedFiles.Count == 0)
            return BadRequest("At least one file is required.");

        var streams = new List<Stream>();
        try
        {
            var files = new List<TransferImportFile>();
            foreach (var file in uploadedFiles)
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                files.Add(new TransferImportFile { Name = file.FileName, Stream = stream });
            }

            var result = await transferService.ImportFilesAsync(files, HttpContext.RequestAborted);
            return ImportResult(result);
        }
        finally
        {
            foreach (var stream in streams)
                await stream.DisposeAsync();
        }
    }

    [HttpPost("import/json")]
    [Consumes("application/json")]
    public async Task<IActionResult> ImportJson()
    {
        try
        {
            var result = await transferService.ImportJsonAsync(Request.Body, HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (Exception exception) when (IsImportRequestFailure(exception))
        {
            return BadRequest(exception.Message);
        }
    }

    private IActionResult ImportResult(TransferImportBatchResult result)
    {
        if (result.FilesImported == 0)
            return BadRequest("No valid import files were found.");

        if (result.FilesFailed > 0)
        {
            Response.Headers["X-SharpOMatic-Import-Failed-Files"] = result.FilesFailed.ToString();
            Response.Headers["X-SharpOMatic-Import-Warning"] = $"{result.FilesFailed} import file(s) failed and were skipped.";
            return StatusCode(StatusCodes.Status207MultiStatus, result.Result);
        }

        return Ok(result.Result);
    }

    private static bool IsImportRequestFailure(Exception exception)
    {
        return exception is SharpOMaticException or JsonException or FormatException or NotSupportedException or InvalidOperationException;
    }
}
