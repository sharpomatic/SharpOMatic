namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssetsController(IRepositoryService repositoryService, IAssetStore assetStore) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AssetSummary>>> GetAssets(
        [FromQuery] AssetScope scope = AssetScope.Library,
        [FromQuery] string? search = null,
        [FromQuery] AssetSortField sortBy = AssetSortField.Name,
        [FromQuery] SortDirection sortDirection = SortDirection.Descending,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 0,
        [FromQuery] Guid? runId = null,
        [FromQuery] Guid? conversationId = null,
        [FromQuery] Guid? folderId = null,
        [FromQuery] bool topLevelOnly = false
    )
    {
        if (scope == AssetScope.Run && runId is null)
            return BadRequest("RunId is required for run scope queries.");

        if (scope == AssetScope.Conversation && conversationId is null)
            return BadRequest("ConversationId is required for conversation scope queries.");

        if ((scope == AssetScope.Run || scope == AssetScope.Conversation) && (folderId.HasValue || topLevelOnly))
            return BadRequest("Folder filters are only valid for library scope queries.");

        if (scope == AssetScope.Library && topLevelOnly && folderId.HasValue)
            return BadRequest("Specify either folderId or topLevelOnly, not both.");

        if (skip < 0)
            skip = 0;

        if (take < 0)
            take = 0;

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var assets = await repositoryService.GetAssetsByScope(scope, normalizedSearch, sortBy, sortDirection, skip, take, runId, conversationId, folderId, topLevelOnly);
        var folderLookup = await BuildFolderNameLookup(assets);
        var summaries = assets
            .Select(asset =>
                new AssetSummary(
                    asset.AssetId,
                    asset.Name,
                    asset.MediaType,
                    asset.SizeBytes,
                    asset.Scope,
                    asset.Created,
                    asset.FolderId,
                    asset.FolderId.HasValue && folderLookup.TryGetValue(asset.FolderId.Value, out var folderName) ? folderName : null
                )
            )
            .ToList();

        return Ok(summaries);
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetAssetCount(
        [FromQuery] AssetScope scope = AssetScope.Library,
        [FromQuery] string? search = null,
        [FromQuery] Guid? runId = null,
        [FromQuery] Guid? conversationId = null,
        [FromQuery] Guid? folderId = null,
        [FromQuery] bool topLevelOnly = false
    )
    {
        if (!Enum.IsDefined(typeof(AssetScope), scope))
            return BadRequest("Scope is invalid.");

        if (scope == AssetScope.Run && runId is null)
            return BadRequest("RunId is required for run scope queries.");

        if (scope == AssetScope.Conversation && conversationId is null)
            return BadRequest("ConversationId is required for conversation scope queries.");

        if ((scope == AssetScope.Run || scope == AssetScope.Conversation) && (folderId.HasValue || topLevelOnly))
            return BadRequest("Folder filters are only valid for library scope queries.");

        if (scope == AssetScope.Library && topLevelOnly && folderId.HasValue)
            return BadRequest("Specify either folderId or topLevelOnly, not both.");

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return await repositoryService.GetAssetCount(scope, normalizedSearch, runId, conversationId, folderId, topLevelOnly);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AssetSummary>> GetAsset(Guid id)
    {
        var asset = await repositoryService.GetAsset(id);
        string? folderName = null;
        if (asset.FolderId.HasValue)
            folderName = (await repositoryService.GetAssetFolder(asset.FolderId.Value)).Name;

        return new AssetSummary(asset.AssetId, asset.Name, asset.MediaType, asset.SizeBytes, asset.Scope, asset.Created, asset.FolderId, folderName);
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
        if (!AssetMediaTypePolicy.IsTextLike(asset.MediaType))
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
        if (!AssetMediaTypePolicy.IsTextLike(asset.MediaType))
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
            ConversationId = asset.ConversationId,
            FolderId = asset.FolderId,
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

        if (request.Scope != AssetScope.Library)
            return BadRequest("Editor uploads only support library assets.");

        if (request.RunId.HasValue)
            return BadRequest("RunId is not valid for editor library uploads.");

        if (request.FolderId.HasValue)
            _ = await repositoryService.GetAssetFolder(request.FolderId.Value);

        var name = string.IsNullOrWhiteSpace(request.Name) ? Path.GetFileName(request.File.FileName) : request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Name is required.");

        var assetId = Guid.NewGuid();
        var storageKey = AssetStorageKey.ForLibrary(assetId, request.FolderId);

        await using (var stream = request.File.OpenReadStream())
            await assetStore.SaveAsync(storageKey, stream, HttpContext.RequestAborted);

        var mediaType = string.IsNullOrWhiteSpace(request.File.ContentType) ? "application/octet-stream" : request.File.ContentType;

        var asset = new Asset
        {
            AssetId = assetId,
            RunId = null,
            ConversationId = null,
            FolderId = request.FolderId,
            Name = name,
            Scope = AssetScope.Library,
            Created = DateTime.Now,
            MediaType = mediaType,
            SizeBytes = request.File.Length,
            StorageKey = storageKey,
        };

        await repositoryService.UpsertAsset(asset);
        string? folderName = request.FolderId.HasValue ? (await repositoryService.GetAssetFolder(request.FolderId.Value)).Name : null;
        return CreatedAtAction(
            nameof(GetAsset),
            new { id = asset.AssetId },
            new AssetSummary(asset.AssetId, asset.Name, asset.MediaType, asset.SizeBytes, asset.Scope, asset.Created, asset.FolderId, folderName)
        );
    }

    [HttpPut("{id}/folder")]
    public async Task<IActionResult> MoveAssetToFolder(Guid id, [FromBody] AssetFolderMoveRequest request)
    {
        var asset = await repositoryService.GetAsset(id);
        if (asset.Scope != AssetScope.Library)
            return BadRequest("Only library assets can be moved between folders.");

        if (asset.FolderId == request.FolderId)
            return NoContent();

        var destinationStorageKey = AssetStorageKey.ForLibrary(asset.AssetId, request.FolderId);
        if (!string.Equals(asset.StorageKey, destinationStorageKey, StringComparison.Ordinal))
            await MoveAssetContent(asset.StorageKey, destinationStorageKey, HttpContext.RequestAborted);

        var updated = new Asset
        {
            AssetId = asset.AssetId,
            RunId = asset.RunId,
            ConversationId = asset.ConversationId,
            FolderId = request.FolderId,
            Name = asset.Name,
            Scope = asset.Scope,
            Created = asset.Created,
            MediaType = asset.MediaType,
            SizeBytes = asset.SizeBytes,
            StorageKey = destinationStorageKey,
        };

        await repositoryService.UpsertAsset(updated);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsset(Guid id)
    {
        var asset = await repositoryService.GetAsset(id);
        await assetStore.DeleteAsync(asset.StorageKey, HttpContext.RequestAborted);
        await repositoryService.DeleteAsset(id);
        return NoContent();
    }

    [HttpGet("folders")]
    public async Task<List<AssetFolderSummary>> GetFolders(
        [FromQuery] string? search = null,
        [FromQuery] SortDirection sortDirection = SortDirection.Ascending,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 0
    )
    {
        if (skip < 0)
            skip = 0;

        if (take < 0)
            take = 0;

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var folders = await repositoryService.GetAssetFolders(normalizedSearch, sortDirection, skip, take);
        return [.. folders.Select(ToSummary)];
    }

    [HttpGet("folders/count")]
    public async Task<int> GetFolderCount([FromQuery] string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return await repositoryService.GetAssetFolderCount(normalizedSearch);
    }

    [HttpGet("folders/{id}")]
    public async Task<ActionResult<AssetFolderSummary>> GetFolder(Guid id)
    {
        var folder = await repositoryService.GetAssetFolder(id);
        return ToSummary(folder);
    }

    [HttpPost("folders")]
    public async Task<ActionResult<AssetFolderSummary>> CreateFolder([FromBody] AssetFolderRequest request)
    {
        if (!TryNormalizeFolderName(request.Name, out var normalizedName, out var validationError))
            return BadRequest(validationError);

        var existingByName = await repositoryService.GetAssetFolderByName(normalizedName);
        if (existingByName is not null)
            return Conflict("A folder with this name already exists.");

        var folder = new AssetFolder { FolderId = Guid.NewGuid(), Name = normalizedName, Created = DateTime.Now };
        await repositoryService.UpsertAssetFolder(folder);

        return CreatedAtAction(nameof(GetFolder), new { id = folder.FolderId }, ToSummary(folder));
    }

    [HttpPut("folders/{id}")]
    public async Task<ActionResult<AssetFolderSummary>> RenameFolder(Guid id, [FromBody] AssetFolderRequest request)
    {
        if (!TryNormalizeFolderName(request.Name, out var normalizedName, out var validationError))
            return BadRequest(validationError);

        var existing = await repositoryService.GetAssetFolder(id);
        if (!string.Equals(existing.Name, normalizedName, StringComparison.Ordinal))
        {
            var existingByName = await repositoryService.GetAssetFolderByName(normalizedName);
            if (existingByName is not null && existingByName.FolderId != id)
                return Conflict("A folder with this name already exists.");
        }

        var updated = new AssetFolder { FolderId = existing.FolderId, Name = normalizedName, Created = existing.Created };
        await repositoryService.UpsertAssetFolder(updated);
        return ToSummary(updated);
    }

    [HttpDelete("folders/{id}")]
    public async Task<IActionResult> DeleteFolder(Guid id)
    {
        var count = await repositoryService.GetAssetFolderAssetCount(id);
        if (count > 0)
            return Conflict("Folder is not empty.");

        await repositoryService.DeleteAssetFolder(id);
        return NoContent();
    }

    private static bool TryNormalizeFolderName(string? name, out string normalizedName, out string? error)
    {
        normalizedName = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Folder name is required.";
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.Contains('/', StringComparison.Ordinal) || trimmed.Contains('\\', StringComparison.Ordinal))
        {
            error = "Folder name cannot contain '/' or '\\'.";
            return false;
        }

        normalizedName = trimmed;
        return true;
    }

    private static AssetFolderSummary ToSummary(AssetFolder folder) => new(folder.FolderId, folder.Name, folder.Created);

    private async Task<Dictionary<Guid, string>> BuildFolderNameLookup(List<Asset> assets)
    {
        var folderIds = assets.Where(asset => asset.FolderId.HasValue).Select(asset => asset.FolderId!.Value).Distinct().ToList();
        var lookup = new Dictionary<Guid, string>();
        foreach (var folderId in folderIds)
        {
            try
            {
                var folder = await repositoryService.GetAssetFolder(folderId);
                lookup[folderId] = folder.Name;
            }
            catch (SharpOMaticException) { }
        }

        return lookup;
    }

    private async Task MoveAssetContent(string sourceStorageKey, string destinationStorageKey, CancellationToken cancellationToken)
    {
        if (assetStore is IAssetStoreMove moveStore)
        {
            await moveStore.MoveAsync(sourceStorageKey, destinationStorageKey, overwrite: true, cancellationToken);
            return;
        }

        await using (var stream = await assetStore.OpenReadAsync(sourceStorageKey, cancellationToken))
        {
            await assetStore.SaveAsync(destinationStorageKey, stream, cancellationToken);
        }

        await assetStore.DeleteAsync(sourceStorageKey, cancellationToken);
    }
}
