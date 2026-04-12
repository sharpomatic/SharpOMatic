namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationController : ControllerBase
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [HttpPost("{workflowId}/{conversationId}")]
    public async Task<ActionResult<Run>> StartOrResumeConversation(
        IEngineService engineService,
        Guid workflowId,
        string conversationId,
        [FromBody] ConversationTurnRequest request
    )
    {
        var resumeInput = ResolveResumeInput(request);
        return await engineService.StartOrResumeConversationAndWait(workflowId, conversationId, resumeInput, request.InputEntries, request.NeedsEditorEvents, request.StreamConversationId);
    }

    [HttpPost("notify/{workflowId}/{conversationId}")]
    public async Task<ActionResult<Guid>> StartOrResumeConversationAndNotify(
        IEngineService engineService,
        Guid workflowId,
        string conversationId,
        [FromBody] ConversationTurnRequest request
    )
    {
        var resumeInput = ResolveResumeInput(request);
        return await engineService.StartOrResumeConversationAndNotify(workflowId, conversationId, resumeInput, request.InputEntries, request.NeedsEditorEvents, request.StreamConversationId);
    }

    [HttpGet("{conversationId}")]
    public async Task<ActionResult<Conversation?>> GetConversation(IRepositoryService repositoryService, string conversationId)
    {
        return await repositoryService.GetConversation(conversationId);
    }

    [HttpGet("workflow/{workflowId}/latest")]
    public async Task<ActionResult<Conversation?>> GetLatestConversationForWorkflow(IRepositoryService repositoryService, Guid workflowId)
    {
        return await repositoryService.GetLatestConversationForWorkflow(workflowId);
    }

    [HttpGet("workflow/{workflowId}/{page}/{count}")]
    public async Task<ActionResult<WorkflowConversationPageResult>> GetConversationsForWorkflow(
        IRepositoryService repositoryService,
        Guid workflowId,
        int page,
        int count,
        [FromQuery] ConversationSortField sortBy = ConversationSortField.Created,
        [FromQuery] SortDirection sortDirection = SortDirection.Descending
    )
    {
        var totalCount = await repositoryService.GetWorkflowConversationCount(workflowId);
        if (count < 1 || page < 1 || totalCount == 0)
            return new WorkflowConversationPageResult([], totalCount);

        var skip = (page - 1) * count;
        var conversations = await repositoryService.GetWorkflowConversations(workflowId, sortBy, sortDirection, skip, count);
        return new WorkflowConversationPageResult(conversations, totalCount);
    }

    [HttpGet("{conversationId}/runs")]
    public async Task<ActionResult<List<Run>>> GetConversationRuns(
        IRepositoryService repositoryService,
        string conversationId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 0
    )
    {
        return await repositoryService.GetConversationRuns(conversationId, skip, take);
    }

    [HttpGet("{conversationId}/history")]
    public async Task<ActionResult<ConversationHistoryResult>> GetConversationHistory(IRepositoryService repositoryService, string conversationId)
    {
        var conversation = await repositoryService.GetConversation(conversationId);
        if (conversation is null)
            return NotFound();

        var runs = await repositoryService.GetConversationRuns(conversationId);
        foreach (var run in runs)
            NormalizeInputEntriesForClient(run);

        var turns = new List<ConversationHistoryTurnResult>(runs.Count);
        foreach (var run in runs)
        {
            var tracesTask = repositoryService.GetRunTraces(run.RunId);
            var informationsTask = repositoryService.GetRunInformations(run.RunId);
            var streamEventsTask = repositoryService.GetRunStreamEvents(run.RunId);
            var runAssetsTask = repositoryService.GetRunAssets(run.RunId);
            await Task.WhenAll(tracesTask, informationsTask, streamEventsTask, runAssetsTask);

            turns.Add(
                new ConversationHistoryTurnResult(
                    run,
                    await tracesTask,
                    await informationsTask,
                    await streamEventsTask,
                    await BuildAssetSummaries(repositoryService, await runAssetsTask)
                )
            );
        }

        var latestRun = runs.LastOrDefault();
        if (conversation.LastRunId.HasValue)
            latestRun = runs.LastOrDefault(r => r.RunId == conversation.LastRunId.Value) ?? latestRun;

        var conversationAssets = await repositoryService.GetAssetsByScope(
            AssetScope.Conversation,
            null,
            AssetSortField.Created,
            SortDirection.Ascending,
            0,
            0,
            conversationId: conversationId
        );

        return Ok(
            new ConversationHistoryResult(
                conversation.ConversationId,
                conversation.WorkflowId,
                latestRun,
                turns,
                await BuildAssetSummaries(repositoryService, conversationAssets)
            )
        );
    }

    private static void NormalizeInputEntriesForClient(Run run)
    {
        if (run.InputEntries is null)
            return;

        var entries = JsonSerializer.Deserialize<ContextEntryListEntity>(run.InputEntries);
        run.InputEntries = JsonSerializer.Serialize(entries, _options);
    }

    private static async Task<List<AssetSummary>> BuildAssetSummaries(IRepositoryService repositoryService, IEnumerable<Asset> assets)
    {
        var assetList = assets.ToList();
        var folderIds = assetList.Where(asset => asset.FolderId.HasValue).Select(asset => asset.FolderId!.Value).Distinct().ToList();
        var folderLookup = new Dictionary<Guid, string>();
        foreach (var folderId in folderIds)
            folderLookup[folderId] = (await repositoryService.GetAssetFolder(folderId)).Name;

        return assetList
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
    }

    private static NodeResumeInput? ResolveResumeInput(ConversationTurnRequest request)
    {
        if (request.ResumeContextJson is null)
            return request.ResumeInput;

        if (request.ResumeInput is not null)
            throw new SharpOMaticException("Resume context json cannot be combined with resume input.");

        try
        {
            var parsed = ContextHelpers.FastDeserializeString(request.ResumeContextJson);
            if (parsed is not ContextObject context)
            {
                if (parsed is ContextList contextList)
                {
                    context = new ContextObject();
                    context.Set("resume", parsed);
                }
                else
                    throw new SharpOMaticException("Resume context json must be a JSON object or list.");
            }

            return new ContextMergeResumeInput() { Context = context };
        }
        catch (Exception ex) when (ex is FastSerializationException or JsonException)
        {
            throw new SharpOMaticException($"Resume context json could not be parsed as json. {ex.Message}");
        }
        catch (SharpOMaticException)
        {
            throw;
        }
    }
}
