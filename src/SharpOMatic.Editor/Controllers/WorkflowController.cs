namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new NodeEntityConverter() } };

    [HttpGet]
    public async Task<ActionResult<List<WorkflowSummary>>> GetWorkflowSummaries(
        IRepositoryService repository,
        [FromQuery] string? search = null,
        [FromQuery] WorkflowSortField sortBy = WorkflowSortField.Name,
        [FromQuery] SortDirection sortDirection = SortDirection.Ascending,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 0,
        [FromQuery] Guid? folderId = null,
        [FromQuery] bool topLevelOnly = false
    )
    {
        if (topLevelOnly && folderId.HasValue)
            return BadRequest("Specify either folderId or topLevelOnly, not both.");

        if (skip < 0)
            skip = 0;

        if (take < 0)
            take = 0;

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return await repository.GetWorkflowSummaries(normalizedSearch, sortBy, sortDirection, skip, take, folderId, topLevelOnly);
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetWorkflowSummaryCount(IRepositoryService repository, [FromQuery] string? search = null, [FromQuery] Guid? folderId = null, [FromQuery] bool topLevelOnly = false)
    {
        if (topLevelOnly && folderId.HasValue)
            return BadRequest("Specify either folderId or topLevelOnly, not both.");

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return await repository.GetWorkflowSummaryCount(normalizedSearch, folderId, topLevelOnly);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowEntity>> GetWorkflow(IRepositoryService repository, Guid id)
    {
        return await repository.GetWorkflow(id);
    }

    [HttpPost()]
    public async Task UpsertWorkflow(IRepositoryService repository)
    {
        string requestBody;
        using var reader = new StreamReader(Request.Body);
        requestBody = await reader.ReadToEndAsync();
        var workflow = JsonSerializer.Deserialize<WorkflowEntity>(requestBody, _options);
        await repository.UpsertWorkflow(workflow!);
    }

    [HttpDelete("{id}")]
    public async Task DeleteWorkflow(IRepositoryService repository, Guid id)
    {
        await repository.DeleteWorkflow(id);
    }

    [HttpPost("copy/{id}")]
    public async Task<ActionResult<Guid>> CopyWorkflow(IRepositoryService repository, Guid id)
    {
        return await repository.CopyWorkflow(id);
    }

    [HttpPut("{id}/folder")]
    public async Task<IActionResult> MoveWorkflowToFolder(IRepositoryService repository, Guid id, [FromBody] WorkflowFolderMoveRequest request)
    {
        await repository.MoveWorkflowToFolder(id, request.WorkflowFolderId);
        return NoContent();
    }

    [HttpGet("folders")]
    public async Task<List<WorkflowFolderSummary>> GetFolders(
        IRepositoryService repository,
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
        var folders = await repository.GetWorkflowFolders(normalizedSearch, sortDirection, skip, take);
        return [.. folders.Select(ToSummary)];
    }

    [HttpGet("folders/count")]
    public async Task<int> GetFolderCount(IRepositoryService repository, [FromQuery] string? search = null)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return await repository.GetWorkflowFolderCount(normalizedSearch);
    }

    [HttpGet("folders/{id}")]
    public async Task<ActionResult<WorkflowFolderSummary>> GetFolder(IRepositoryService repository, Guid id)
    {
        var folder = await repository.GetWorkflowFolder(id);
        return ToSummary(folder);
    }

    [HttpPost("folders")]
    public async Task<ActionResult<WorkflowFolderSummary>> CreateFolder(IRepositoryService repository, [FromBody] WorkflowFolderRequest request)
    {
        var folder = new WorkflowFolder
        {
            WorkflowFolderId = Guid.NewGuid(),
            Name = request.Name,
            Created = DateTime.Now,
        };
        await repository.UpsertWorkflowFolder(folder);
        return CreatedAtAction(nameof(GetFolder), new { id = folder.WorkflowFolderId }, ToSummary(folder));
    }

    [HttpPut("folders/{id}")]
    public async Task<ActionResult<WorkflowFolderSummary>> RenameFolder(IRepositoryService repository, Guid id, [FromBody] WorkflowFolderRequest request)
    {
        var existing = await repository.GetWorkflowFolder(id);
        var updated = new WorkflowFolder
        {
            WorkflowFolderId = existing.WorkflowFolderId,
            Name = request.Name,
            Created = existing.Created,
        };
        await repository.UpsertWorkflowFolder(updated);
        return ToSummary(updated);
    }

    [HttpDelete("folders/{id}")]
    public async Task<IActionResult> DeleteFolder(IRepositoryService repository, Guid id)
    {
        await repository.DeleteWorkflowFolder(id);
        return NoContent();
    }

    [HttpPost("run/{id}")]
    public async Task<ActionResult<Guid>> Run(IEngineService engineService, Guid id)
    {
        // Parse the incoming ContextEntryListEntity data
        using var reader = new StreamReader(Request.Body);
        var contextEntryListEntity = JsonSerializer.Deserialize<ContextEntryListEntity>(await reader.ReadToEndAsync(), _options);

        var runId = await engineService.StartWorkflowRunAndNotify(id, inputEntries: contextEntryListEntity, needsEditorEvents: true);
        return runId;
    }

    private static WorkflowFolderSummary ToSummary(WorkflowFolder folder) =>
        new()
        {
            WorkflowFolderId = folder.WorkflowFolderId,
            Name = folder.Name,
            Created = folder.Created,
        };
}
