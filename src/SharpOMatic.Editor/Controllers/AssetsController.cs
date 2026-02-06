namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController(IRepositoryService repositoryService, IAssetStore assetStore) : ControllerBase
{
    private static readonly HashSet<string> EditableTextMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/markdown",
        "text/csv",
        "text/html",
        "text/xml",
        "text/css",
        "text/javascript",
        "application/json",
        "application/xml",
        "application/x-yaml",
        "application/javascript",
    };

    [HttpGet]
    public async Task<List<AssetSummary>> GetAssets(
        [FromQuery] AssetScope scope = AssetScope.Library,
        [FromQuery] string? search = null,
        [FromQuery] AssetSortField sortBy = AssetSortField.Name,
        [FromQuery] SortDirection sortDirection = SortDirection.Descending,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 0,
        [FromQuery] Guid? runId = null
    )
    {
        if (skip < 0)
            skip = 0;

        if (take < 0)
            take = 0;

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var assets = await repositoryService.GetAssetsByScope(scope, normalizedSearch, sortBy, sortDirection, skip, take, runId);
        return [.. assets.Select(ToSummary)];
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetAssetCount([FromQuery] AssetScope scope = AssetScope.Library, [FromQuery] string? search = null, [FromQuery] Guid? runId = null)
    {
        if (!Enum.IsDefined(typeof(AssetScope), scope))
            return BadRequest("Scope is invalid.");

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return await repositoryService.GetAssetCount(scope, normalizedSearch, runId);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AssetSummary>> GetAsset(Guid id)
    {
        var asset = await repositoryService.GetAsset(id);
        return ToSummary(asset);
    }

    [HttpGet("{id}/content")]
    public async Task<IActionResult> GetAssetContent(Guid id)
    {
        var asset = await repositoryService.GetAsset(id);
        var stream = await assetStore.OpenReadAsync(asset.StorageKey, HttpContext.RequestAborted);
        return File(stream, asset.MediaType, enableRangeProcessing: true);
    }

    [HttpGet("{id}/text")]
    public async Task<ActionResult<AssetTextRequest>> GetAssetText(Guid id)
    {
        var asset = await repositoryService.GetAsset(id);
        if (!IsEditableTextMediaType(asset.MediaType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, "Media type is not editable.");

        await using var stream = await assetStore.OpenReadAsync(asset.StorageKey, HttpContext.RequestAborted);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(HttpContext.RequestAborted);

        return new AssetTextRequest { Content = content };
    }

    [HttpPut("{id}/text")]
    public async Task<IActionResult> UpdateAssetText(Guid id, [FromBody] AssetTextRequest request)
    {
        var asset = await repositoryService.GetAsset(id);
        if (!IsEditableTextMediaType(asset.MediaType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, "Media type is not editable.");

        var text = request.Content ?? string.Empty;
        var bytes = Encoding.UTF8.GetBytes(text);
        await using (var contentStream = new MemoryStream(bytes))
        {
            await assetStore.SaveAsync(asset.StorageKey, contentStream, HttpContext.RequestAborted);
        }

        var updated = new Asset
        {
            AssetId = asset.AssetId,
            RunId = asset.RunId,
            Name = asset.Name,
            Scope = asset.Scope,
            Created = asset.Created,
            MediaType = asset.MediaType,
            SizeBytes = bytes.Length,
            StorageKey = asset.StorageKey,
        };

        await repositoryService.UpsertAsset(updated);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<AssetSummary>> UploadAsset([FromForm] AssetUploadRequest request)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest("File is required.");

        if (!Enum.IsDefined(typeof(AssetScope), request.Scope))
            return BadRequest("Scope is invalid.");

        if (request.Scope == AssetScope.Run && request.RunId is null)
            return BadRequest("RunId is required for run assets.");

        var name = string.IsNullOrWhiteSpace(request.Name) ? Path.GetFileName(request.File.FileName) : request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Name is required.");

        var assetId = Guid.NewGuid();
        var storageKey = AssetStorageKey.ForScope(request.Scope, assetId, request.RunId);

        await using (var stream = request.File.OpenReadStream())
            await assetStore.SaveAsync(storageKey, stream, HttpContext.RequestAborted);

        var mediaType = string.IsNullOrWhiteSpace(request.File.ContentType) ? "application/octet-stream" : request.File.ContentType;

        var asset = new Asset
        {
            AssetId = assetId,
            RunId = request.Scope == AssetScope.Run ? request.RunId : null,
            Name = name,
            Scope = request.Scope,
            Created = DateTime.Now,
            MediaType = mediaType,
            SizeBytes = request.File.Length,
            StorageKey = storageKey,
        };

        await repositoryService.UpsertAsset(asset);

        return CreatedAtAction(nameof(GetAsset), new { id = asset.AssetId }, ToSummary(asset));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsset(Guid id)
    {
        var asset = await repositoryService.GetAsset(id);
        await assetStore.DeleteAsync(asset.StorageKey, HttpContext.RequestAborted);
        await repositoryService.DeleteAsset(id);
        return NoContent();
    }

    private static AssetSummary ToSummary(Asset asset) => new(asset.AssetId, asset.Name, asset.MediaType, asset.SizeBytes, asset.Scope, asset.Created);

    private static bool IsEditableTextMediaType(string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
            return false;

        var normalized = mediaType.Split(';', 2)[0].Trim();
        return EditableTextMediaTypes.Contains(normalized);
    }
}
