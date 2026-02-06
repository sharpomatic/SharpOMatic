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

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TransferImportResult>> Import([FromForm] TransferImportRequest request)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest("File is required.");

        await using var stream = request.File.OpenReadStream();
        var result = await transferService.ImportAsync(stream, HttpContext.RequestAborted);
        return Ok(result);
    }
}
