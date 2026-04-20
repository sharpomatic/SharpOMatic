namespace SharpOMatic.Tests.Services;

public sealed class TestRepositoryService : IRepositoryService
{
    private readonly ConcurrentDictionary<string, Setting> _settings = new();
    private readonly ConcurrentDictionary<Guid, WorkflowEntity> _workflows = new();
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new();
    private readonly ConcurrentDictionary<string, ConversationCheckpoint> _conversationCheckpoints = new();
    private readonly ConcurrentDictionary<Guid, Run> _runs = new();
    private readonly ConcurrentDictionary<Guid, Trace> _traces = new();
    private readonly ConcurrentDictionary<Guid, Information> _informations = new();
    private readonly ConcurrentDictionary<Guid, StreamEvent> _streamEvents = new();
    private readonly ConcurrentDictionary<Guid, Asset> _assets = new();
    private readonly ConcurrentDictionary<Guid, AssetFolder> _assetFolders = new();
    private readonly ConcurrentDictionary<string, ConnectorConfig> _connectorConfigs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, Connector> _connectors = new();
    private readonly ConcurrentDictionary<string, ModelConfig> _modelConfigs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, Model> _models = new();

    public Task<List<WorkflowSummary>> GetWorkflowSummaries() => throw new NotImplementedException();

    public Task<int> GetWorkflowSummaryCount(string? search) => throw new NotImplementedException();

    public Task<List<WorkflowSummary>> GetWorkflowSummaries(string? search, WorkflowSortField sortBy, SortDirection sortDirection, int skip, int take) => throw new NotImplementedException();

    public Task<WorkflowEntity> GetWorkflow(Guid workflowId)
    {
        if (!_workflows.TryGetValue(workflowId, out var workflowEntity))
            throw new ApplicationException($"GetWorkflow failed for '{workflowId}'");

        return Task.FromResult(workflowEntity);
    }

    public Task UpsertWorkflow(WorkflowEntity workflow)
    {
        _workflows[workflow.Id] = workflow;
        return Task.CompletedTask;
    }

    public Task DeleteWorkflow(Guid workflowId) => throw new NotImplementedException();

    public Task<Guid> CopyWorkflow(Guid workflowId) => throw new NotImplementedException();

    public Task<Conversation?> GetLatestConversationForWorkflow(Guid workflowId)
    {
        return Task.FromResult(_conversations.Values.Where(c => c.WorkflowId == workflowId).OrderByDescending(c => c.Updated).ThenByDescending(c => c.Created).FirstOrDefault());
    }

    public Task<Conversation?> GetConversation(string conversationId)
    {
        _conversations.TryGetValue(conversationId, out var conversation);
        return Task.FromResult(conversation);
    }

    public Task<int> GetWorkflowConversationCount(Guid workflowId)
    {
        return Task.FromResult(_conversations.Values.Count(c => c.WorkflowId == workflowId));
    }

    public Task<List<Conversation>> GetWorkflowConversations(Guid workflowId, ConversationSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        IEnumerable<Conversation> conversations = _conversations.Values.Where(c => c.WorkflowId == workflowId);

        conversations = sortBy switch
        {
            ConversationSortField.Created => sortDirection == SortDirection.Ascending
                ? conversations.OrderBy(c => c.Created).ThenByDescending(c => c.Updated)
                : conversations.OrderByDescending(c => c.Created).ThenByDescending(c => c.Updated),
            ConversationSortField.Status => sortDirection == SortDirection.Ascending
                ? conversations.OrderBy(c => c.Status).ThenByDescending(c => c.Updated)
                : conversations.OrderByDescending(c => c.Status).ThenByDescending(c => c.Updated),
            _ => sortDirection == SortDirection.Ascending
                ? conversations.OrderBy(c => c.Updated).ThenByDescending(c => c.Created)
                : conversations.OrderByDescending(c => c.Updated).ThenByDescending(c => c.Created),
        };

        if (skip > 0)
            conversations = conversations.Skip(skip);

        if (take > 0)
            conversations = conversations.Take(take);

        return Task.FromResult(conversations.ToList());
    }

    public Task UpsertConversation(Conversation conversation)
    {
        _conversations[conversation.ConversationId] = conversation;
        return Task.CompletedTask;
    }

    public Task<ConversationCheckpoint?> GetConversationCheckpoint(string conversationId)
    {
        _conversationCheckpoints.TryGetValue(conversationId, out var checkpoint);
        return Task.FromResult(checkpoint);
    }

    public Task UpsertConversationCheckpoint(ConversationCheckpoint checkpoint)
    {
        _conversationCheckpoints[checkpoint.ConversationId] = checkpoint;
        return Task.CompletedTask;
    }

    public Task DeleteConversationCheckpoint(string conversationId)
    {
        _conversationCheckpoints.TryRemove(conversationId, out _);
        return Task.CompletedTask;
    }

    public Task<List<Run>> GetConversationRuns(string conversationId, int skip = 0, int take = 0)
    {
        IEnumerable<Run> runs = _runs.Values.Where(r => r.ConversationId == conversationId).OrderBy(r => r.TurnNumber).ThenBy(r => r.Created);
        if (skip > 0)
            runs = runs.Skip(skip);
        if (take > 0)
            runs = runs.Take(take);

        return Task.FromResult(runs.ToList());
    }

    public Task<bool> TryAcquireConversationLease(string conversationId, string leaseOwner, DateTime leaseExpiresUtc)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
            return Task.FromResult(false);

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(conversation.LeaseOwner) && conversation.LeaseOwner != leaseOwner && conversation.LeaseExpires.HasValue && conversation.LeaseExpires.Value > now)
            return Task.FromResult(false);

        conversation.LeaseOwner = leaseOwner;
        conversation.LeaseExpires = leaseExpiresUtc;
        _conversations[conversationId] = conversation;
        return Task.FromResult(true);
    }

    public Task ReleaseConversationLease(string conversationId, string leaseOwner)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation) && conversation.LeaseOwner == leaseOwner)
        {
            conversation.LeaseOwner = null;
            conversation.LeaseExpires = null;
            _conversations[conversationId] = conversation;
        }

        return Task.CompletedTask;
    }

    public Task PruneWorkflowConversations(Guid workflowId, int keepLatest)
    {
        var conversationsToDelete = _conversations.Values
            .Where(c => c.WorkflowId == workflowId && c.Status != ConversationStatus.Suspended && c.Status != ConversationStatus.Running)
            .OrderByDescending(c => c.Updated)
            .Skip(keepLatest)
            .Select(c => c.ConversationId)
            .ToList();

        foreach (var conversationId in conversationsToDelete)
        {
            _conversationCheckpoints.TryRemove(conversationId, out _);

            var runIds = _runs.Values.Where(r => r.ConversationId == conversationId).Select(r => r.RunId).ToList();
            foreach (var runId in runIds)
            {
                _runs.TryRemove(runId, out _);

                foreach (var trace in _traces.Where(t => t.Value.RunId == runId).Select(t => t.Key).ToList())
                    _traces.TryRemove(trace, out _);

                foreach (var information in _informations.Where(i => i.Value.RunId == runId).Select(i => i.Key).ToList())
                    _informations.TryRemove(information, out _);

                foreach (var streamEvent in _streamEvents.Where(s => s.Value.RunId == runId).Select(s => s.Key).ToList())
                    _streamEvents.TryRemove(streamEvent, out _);

                foreach (var asset in _assets.Where(a => a.Value.RunId == runId).Select(a => a.Key).ToList())
                    _assets.TryRemove(asset, out _);
            }

            foreach (var asset in _assets.Where(a => a.Value.ConversationId == conversationId).Select(a => a.Key).ToList())
                _assets.TryRemove(asset, out _);

            _conversations.TryRemove(conversationId, out _);
        }

        return Task.CompletedTask;
    }

    public Task<Run?> GetRun(Guid runId)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public Task<Run?> GetLatestRunForWorkflow(Guid workflowId)
    {
        return Task.FromResult(_runs.Values.Where(r => r.WorkflowId == workflowId).OrderByDescending(r => r.Created).FirstOrDefault());
    }

    public Task<int> GetWorkflowRunCount(Guid workflowId)
    {
        return Task.FromResult(_runs.Values.Count(r => r.WorkflowId == workflowId));
    }

    public Task<List<Run>> GetWorkflowRuns(Guid workflowId, RunSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        IEnumerable<Run> runs = _runs.Values.Where(r => r.WorkflowId == workflowId);

        runs = sortBy switch
        {
            RunSortField.Status => sortDirection == SortDirection.Ascending
                ? runs.OrderBy(r => r.RunStatus).ThenByDescending(r => r.Created)
                : runs.OrderByDescending(r => r.RunStatus).ThenByDescending(r => r.Created),
            _ => sortDirection == SortDirection.Ascending
                ? runs.OrderBy(r => r.Created)
                : runs.OrderByDescending(r => r.Created)
        };

        if (skip > 0)
            runs = runs.Skip(skip);

        if (take > 0)
            runs = runs.Take(take);

        return Task.FromResult(runs.ToList());
    }

    public Task UpsertRun(Run run)
    {
        _runs[run.RunId] = run;
        return Task.CompletedTask;
    }

    public Task PruneWorkflowRuns(Guid workflowId, int keepLatest)
    {
        var runIdsToDelete = _runs.Values
            .Where(r => r.WorkflowId == workflowId && r.ConversationId == null)
            .OrderByDescending(r => r.Created)
            .Skip(keepLatest)
            .Select(r => r.RunId)
            .ToList();

        foreach (var runId in runIdsToDelete)
        {
            _runs.TryRemove(runId, out _);

            foreach (var trace in _traces.Where(t => t.Value.RunId == runId).Select(t => t.Key).ToList())
                _traces.TryRemove(trace, out _);

            foreach (var information in _informations.Where(i => i.Value.RunId == runId).Select(i => i.Key).ToList())
                _informations.TryRemove(information, out _);

            foreach (var streamEvent in _streamEvents.Where(s => s.Value.RunId == runId).Select(s => s.Key).ToList())
                _streamEvents.TryRemove(streamEvent, out _);

            foreach (var asset in _assets.Where(a => a.Value.RunId == runId).Select(a => a.Key).ToList())
                _assets.TryRemove(asset, out _);
        }

        return Task.CompletedTask;
    }

    public Task<List<Trace>> GetRunTraces(Guid runId)
    {
        var traces = _traces.Where(t => t.Value.RunId == runId).OrderBy(t => t.Value.Created).Select(t => t.Value).ToList();
        return Task.FromResult(traces);
    }

    public Task UpsertTrace(Trace trace)
    {
        _traces[trace.TraceId] = trace;
        return Task.CompletedTask;
    }

    public Task<List<Information>> GetRunInformations(Guid runId)
    {
        var informations = _informations.Where(i => i.Value.RunId == runId).OrderBy(i => i.Value.Created).Select(i => i.Value).ToList();
        return Task.FromResult(informations);
    }

    public Task UpsertInformations(List<Information> informations)
    {
        foreach (var information in informations)
            _informations[information.InformationId] = information;

        return Task.CompletedTask;
    }

    public Task<int> GetNextStreamSequence(Guid runId, string? conversationId)
    {
        var maxSequence = !string.IsNullOrWhiteSpace(conversationId)
            ? _streamEvents.Values.Where(e => e.ConversationId == conversationId).Select(e => (int?)e.SequenceNumber).DefaultIfEmpty(null).Max()
            : _streamEvents.Values.Where(e => e.RunId == runId).Select(e => (int?)e.SequenceNumber).DefaultIfEmpty(null).Max();

        return Task.FromResult((maxSequence ?? 0) + 1);
    }

    public Task AppendStreamEvents(List<StreamEvent> events)
    {
        foreach (var streamEvent in events)
            _streamEvents[streamEvent.StreamEventId] = streamEvent;

        return Task.CompletedTask;
    }

    public Task UpdateStreamEventsHideFromReply(List<Guid> streamEventIds, bool hideFromReply)
    {
        foreach (var streamEventId in streamEventIds)
        {
            if (_streamEvents.TryGetValue(streamEventId, out var streamEvent))
            {
                streamEvent.HideFromReply = hideFromReply;
                _streamEvents[streamEventId] = streamEvent;
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<StreamEvent>> GetRunStreamEvents(Guid runId)
    {
        var streamEvents = _streamEvents.Values.Where(e => e.RunId == runId && !e.HideFromReply).OrderBy(e => e.SequenceNumber).ThenBy(e => e.Created).ToList();
        return Task.FromResult(streamEvents);
    }

    public Task<List<StreamEvent>> GetConversationStreamEvents(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new SharpOMaticException("Conversation stream id cannot be empty.");

        var streamEvents = _streamEvents.Values.Where(e => e.ConversationId == conversationId && !e.HideFromReply).OrderBy(e => e.SequenceNumber).ThenBy(e => e.Created).ToList();
        return Task.FromResult(streamEvents);
    }

    public Task<List<ConnectorConfig>> GetConnectorConfigs() => Task.FromResult(_connectorConfigs.Values.OrderBy(c => c.DisplayName).ToList());

    public Task<ConnectorConfig?> GetConnectorConfig(string configId)
    {
        _connectorConfigs.TryGetValue(configId, out var config);
        return Task.FromResult(config);
    }

    public Task UpsertConnectorConfig(ConnectorConfig config)
    {
        _connectorConfigs[config.ConfigId] = config;
        return Task.CompletedTask;
    }

    public Task<List<ConnectorSummary>> GetConnectorSummaries() => throw new NotImplementedException();

    public Task<int> GetConnectorSummaryCount(string? search) => throw new NotImplementedException();

    public Task<List<ConnectorSummary>> GetConnectorSummaries(string? search, ConnectorSortField sortBy, SortDirection sortDirection, int skip, int take) => throw new NotImplementedException();

    public Task<Connector> GetConnector(Guid connectionId, bool hideSecrets = true)
    {
        if (!_connectors.TryGetValue(connectionId, out var connector))
            throw new SharpOMaticException($"Connector '{connectionId}' cannot be found.");

        return Task.FromResult(connector);
    }

    public Task UpsertConnector(Connector connection, bool hideSecrets = true)
    {
        _connectors[connection.ConnectorId] = connection;
        return Task.CompletedTask;
    }

    public Task DeleteConnector(Guid connectionId) => throw new NotImplementedException();

    public Task<List<ModelConfig>> GetModelConfigs() => Task.FromResult(_modelConfigs.Values.OrderBy(c => c.DisplayName).ToList());

    public Task<ModelConfig?> GetModelConfig(string configId)
    {
        _modelConfigs.TryGetValue(configId, out var config);
        return Task.FromResult(config);
    }

    public Task UpsertModelConfig(ModelConfig config)
    {
        _modelConfigs[config.ConfigId] = config;
        return Task.CompletedTask;
    }

    public Task<List<ModelSummary>> GetModelSummaries() => throw new NotImplementedException();

    public Task<int> GetModelSummaryCount(string? search) => throw new NotImplementedException();

    public Task<List<ModelSummary>> GetModelSummaries(string? search, ModelSortField sortBy, SortDirection sortDirection, int skip, int take) => throw new NotImplementedException();

    public Task<Model> GetModel(Guid modelId, bool hideSecrets = true)
    {
        if (!_models.TryGetValue(modelId, out var model))
            throw new SharpOMaticException($"Model '{modelId}' cannot be found.");

        return Task.FromResult(model);
    }

    public Task UpsertModel(Model model)
    {
        _models[model.ModelId] = model;
        return Task.CompletedTask;
    }

    public Task DeleteModel(Guid modelId) => throw new NotImplementedException();

    public Task<List<EvalConfigSummary>> GetEvalConfigSummaries() => throw new NotImplementedException();

    public Task<int> GetEvalConfigSummaryCount(string? search) => throw new NotImplementedException();

    public Task<List<EvalConfigSummary>> GetEvalConfigSummaries(string? search, EvalConfigSortField sortBy, SortDirection sortDirection, int skip, int take) => throw new NotImplementedException();

    public Task<EvalConfig> GetEvalConfig(Guid evalConfigId) => throw new NotImplementedException();

    public Task<EvalConfigDetail> GetEvalConfigDetail(Guid evalConfigId) => throw new NotImplementedException();

    public Task UpsertEvalConfig(EvalConfig evalConfig) => throw new NotImplementedException();

    public Task DeleteEvalConfig(Guid evalConfigId) => throw new NotImplementedException();

    public Task UpsertEvalGraders(List<EvalGrader> graders) => throw new NotImplementedException();

    public Task DeleteEvalGrader(Guid evalGraderId) => throw new NotImplementedException();

    public Task UpsertEvalColumns(List<EvalColumn> columns) => throw new NotImplementedException();

    public Task DeleteEvalColumn(Guid evalColumnId) => throw new NotImplementedException();

    public Task UpsertEvalRows(List<EvalRow> rows) => throw new NotImplementedException();

    public Task DeleteEvalRow(Guid evalRowId) => throw new NotImplementedException();

    public Task UpsertEvalData(List<EvalData> data) => throw new NotImplementedException();

    public Task DeleteEvalData(Guid evalDataId) => throw new NotImplementedException();

    public Task RequestCancelEvalRun(Guid evalRunId) => throw new NotImplementedException();

    public Task RenameEvalRun(Guid evalRunId, string name) => throw new NotImplementedException();

    public Task MoveEvalRun(Guid evalRunId, MoveDirection direction) => throw new NotImplementedException();

    public Task DeleteEvalRun(Guid evalRunId) => throw new NotImplementedException();

    public Task<bool> UpsertEvalRun(EvalRun evalRun, bool allowInsert = true) => throw new NotImplementedException();

    public Task<bool> UpsertEvalRunRows(List<EvalRunRow> runRows, bool allowInsert = true) => throw new NotImplementedException();

    public Task<bool> UpsertEvalRunRowGraders(List<EvalRunRowGrader> runRowGraders, bool allowInsert = true) => throw new NotImplementedException();

    public Task UpsertEvalRunGraderSummaries( List<EvalRunGraderSummary> graderSummaries) => throw new NotImplementedException();

    public Task<int> GetEvalRunSummaryCount(Guid evalConfigId, string? search) => throw new NotImplementedException();

    public Task<List<EvalRunSummary>> GetEvalRunSummaries(Guid evalConfigId, string? search, EvalRunSortField sortBy, SortDirection sortDirection, int skip, int take) =>
        throw new NotImplementedException();

    public async Task<EvalRun> GetEvalRun(Guid evalRunId) => throw new NotImplementedException();

    public Task<EvalRunDetail> GetEvalRunDetail(Guid evalRunId) => throw new NotImplementedException();

    public Task<int> GetEvalRunRowCount(Guid evalRunId, string? search) => throw new NotImplementedException();

    public Task<List<EvalRunRowGrader>> GetEvalRunRowGraders(Guid evalRunId, Guid evalGraderId) => throw new NotImplementedException();

    public Task<List<EvalRunRowDetail>> GetEvalRunRows(Guid evalRunId, string? search, EvalRunRowSortField sortBy, SortDirection sortDirection, int skip, int take) =>
        throw new NotImplementedException();

    public Task<List<Setting>> GetSettings() => throw new NotImplementedException();

    public Task<Setting?> GetSetting(string name)
    {
        _settings.TryGetValue(name, out var setting);
        return Task.FromResult(setting);
    }

    public Task UpsertSetting(Setting model)
    {
        _settings[model.Name] = model;
        return Task.CompletedTask;
    }

    public Task<Asset> GetAsset(Guid assetId)
    {
        if (!_assets.TryGetValue(assetId, out var asset))
            throw new SharpOMaticException($"Asset '{assetId}' cannot be found.");

        return Task.FromResult(asset);
    }

    public Task<int> GetAssetCount(AssetScope scope, string? search, Guid? runId = null, string? conversationId = null, Guid? folderId = null, bool topLevelOnly = false)
    {
        return Task.FromResult(FilterAssets(scope, search, runId, conversationId, folderId, topLevelOnly).Count());
    }

    public Task<List<Asset>> GetAssetsByScope(
        AssetScope scope,
        string? search,
        AssetSortField sortBy,
        SortDirection sortDirection,
        int skip,
        int take,
        Guid? runId = null,
        string? conversationId = null,
        Guid? folderId = null,
        bool topLevelOnly = false
    )
    {
        IEnumerable<Asset> assets = FilterAssets(scope, search, runId, conversationId, folderId, topLevelOnly).OrderByDescending(a => a.Created);
        if (skip > 0)
            assets = assets.Skip(skip);
        if (take > 0)
            assets = assets.Take(take);

        return Task.FromResult(assets.ToList());
    }

    public Task<List<Asset>> GetRunAssets(Guid runId)
    {
        return Task.FromResult(_assets.Values.Where(a => a.RunId == runId).OrderByDescending(a => a.Created).ToList());
    }

    public Task<Asset?> GetRunAssetByName(Guid runId, string name)
    {
        return Task.FromResult(_assets.Values.Where(a => a.Scope == AssetScope.Run && a.RunId == runId && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(a => a.Created).FirstOrDefault());
    }

    public Task<Asset?> GetConversationAssetByName(string conversationId, string name)
    {
        return Task.FromResult(_assets.Values.Where(a => a.Scope == AssetScope.Conversation && a.ConversationId == conversationId && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(a => a.Created).FirstOrDefault());
    }

    public Task<Asset?> GetLibraryAssetByFolderAndName(string folderName, string name)
    {
        var folder = _assetFolders.Values.FirstOrDefault(f => f.Name.Equals(folderName, StringComparison.Ordinal));
        if (folder is null)
            return Task.FromResult<Asset?>(null);

        return Task.FromResult(_assets.Values.Where(a => a.Scope == AssetScope.Library && a.FolderId == folder.FolderId && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(a => a.Created).FirstOrDefault());
    }

    public Task<Asset?> GetLibraryAssetByName(string name)
    {
        return Task.FromResult(_assets.Values.Where(a => a.Scope == AssetScope.Library && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(a => a.Created).FirstOrDefault());
    }

    public Task UpsertAsset(Asset asset)
    {
        _assets[asset.AssetId] = asset;
        return Task.CompletedTask;
    }

    public Task DeleteAsset(Guid assetId)
    {
        _assets.TryRemove(assetId, out _);
        return Task.CompletedTask;
    }

    public Task<AssetFolder> GetAssetFolder(Guid folderId)
    {
        if (!_assetFolders.TryGetValue(folderId, out var folder))
            throw new SharpOMaticException($"Asset folder '{folderId}' cannot be found.");

        return Task.FromResult(folder);
    }

    public Task<AssetFolder?> GetAssetFolderByName(string name)
    {
        return Task.FromResult(_assetFolders.Values.FirstOrDefault(f => f.Name.Equals(name, StringComparison.Ordinal)));
    }

    public Task<int> GetAssetFolderCount(string? search)
    {
        return Task.FromResult(_assetFolders.Count);
    }

    public Task<List<AssetFolder>> GetAssetFolders(string? search, SortDirection sortDirection, int skip, int take)
    {
        IEnumerable<AssetFolder> folders = _assetFolders.Values.OrderBy(f => f.Name);
        if (skip > 0)
            folders = folders.Skip(skip);
        if (take > 0)
            folders = folders.Take(take);

        return Task.FromResult(folders.ToList());
    }

    public Task<int> GetAssetFolderAssetCount(Guid folderId)
    {
        return Task.FromResult(_assets.Values.Count(a => a.Scope == AssetScope.Library && a.FolderId == folderId));
    }

    public Task UpsertAssetFolder(AssetFolder folder)
    {
        _assetFolders[folder.FolderId] = folder;
        return Task.CompletedTask;
    }

    public Task DeleteAssetFolder(Guid folderId)
    {
        _assetFolders.TryRemove(folderId, out _);
        return Task.CompletedTask;
    }

    private IEnumerable<Asset> FilterAssets(AssetScope scope, string? search, Guid? runId, string? conversationId, Guid? folderId, bool topLevelOnly)
    {
        IEnumerable<Asset> assets = _assets.Values.Where(a => a.Scope == scope);
        if (scope == AssetScope.Run && runId.HasValue)
            assets = assets.Where(a => a.RunId == runId);
        else if (scope == AssetScope.Conversation && !string.IsNullOrWhiteSpace(conversationId))
            assets = assets.Where(a => a.ConversationId == conversationId);
        else if (scope == AssetScope.Library)
        {
            if (topLevelOnly)
                assets = assets.Where(a => a.FolderId == null);
            else if (folderId.HasValue)
                assets = assets.Where(a => a.FolderId == folderId);
        }

        if (!string.IsNullOrWhiteSpace(search))
            assets = assets.Where(a => a.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        return assets;
    }
}
