namespace SharpOMatic.Tests.Services;

public sealed class TestRepositoryService : IRepositoryService
{
    private readonly ConcurrentDictionary<string, Setting> _settings = new();
    private readonly ConcurrentDictionary<Guid, WorkflowEntity> _workflows = new();
    private readonly ConcurrentDictionary<Guid, Run> _runs = new();
    private readonly ConcurrentDictionary<Guid, Trace> _traces = new();
    private readonly ConcurrentDictionary<Guid, Information> _informations = new();

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

    public Task<Run?> GetRun(Guid runId)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public Task<Run?> GetLatestRunForWorkflow(Guid workflowId) => throw new NotImplementedException();

    public Task<int> GetWorkflowRunCount(Guid workflowId) => throw new NotImplementedException();

    public Task<List<Run>> GetWorkflowRuns(Guid workflowId, RunSortField sortBy, SortDirection sortDirection, int skip, int take) => throw new NotImplementedException();

    public Task UpsertRun(Run run)
    {
        _runs[run.RunId] = run;
        return Task.CompletedTask;
    }

    public Task PruneWorkflowRuns(Guid workflowId, int keepLatest)
    {
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

    public Task<List<ConnectorConfig>> GetConnectorConfigs() => throw new NotImplementedException();

    public Task<ConnectorConfig?> GetConnectorConfig(string configId) => throw new NotImplementedException();

    public Task UpsertConnectorConfig(ConnectorConfig config) => throw new NotImplementedException();

    public Task<List<ConnectorSummary>> GetConnectorSummaries() => throw new NotImplementedException();

    public Task<int> GetConnectorSummaryCount(string? search) => throw new NotImplementedException();

    public Task<List<ConnectorSummary>> GetConnectorSummaries(string? search, ConnectorSortField sortBy, SortDirection sortDirection, int skip, int take) => throw new NotImplementedException();

    public Task<Connector> GetConnector(Guid connectionId, bool hideSecrets = true) => throw new NotImplementedException();

    public Task UpsertConnector(Connector connection, bool hideSecrets = true) => throw new NotImplementedException();

    public Task DeleteConnector(Guid connectionId) => throw new NotImplementedException();

    public Task<List<ModelConfig>> GetModelConfigs() => throw new NotImplementedException();

    public Task<ModelConfig?> GetModelConfig(string configId) => throw new NotImplementedException();

    public Task UpsertModelConfig(ModelConfig config) => throw new NotImplementedException();

    public Task<List<ModelSummary>> GetModelSummaries() => throw new NotImplementedException();

    public Task<int> GetModelSummaryCount(string? search) => throw new NotImplementedException();

    public Task<List<ModelSummary>> GetModelSummaries(string? search, ModelSortField sortBy, SortDirection sortDirection, int skip, int take) => throw new NotImplementedException();

    public Task<Model> GetModel(Guid modelId, bool hideSecrets = true) => throw new NotImplementedException();

    public Task UpsertModel(Model model) => throw new NotImplementedException();

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

    public Task UpsertEvalRun(EvalRun evalRun) => throw new NotImplementedException();

    public Task UpsertEvalRunRows(List<EvalRunRow> runRows) => throw new NotImplementedException();

    public Task UpsertEvalRunRowGraders(List<EvalRunRowGrader> runRowGraders) => throw new NotImplementedException();

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

    public Task<Asset> GetAsset(Guid assetId) => throw new NotImplementedException();

    public Task<int> GetAssetCount(AssetScope scope, string? search, Guid? runId = null, Guid? folderId = null, bool topLevelOnly = false) => throw new NotImplementedException();

    public Task<List<Asset>> GetAssetsByScope(
        AssetScope scope,
        string? search,
        AssetSortField sortBy,
        SortDirection sortDirection,
        int skip,
        int take,
        Guid? runId = null,
        Guid? folderId = null,
        bool topLevelOnly = false
    ) =>
        throw new NotImplementedException();

    public Task<List<Asset>> GetRunAssets(Guid runId) => throw new NotImplementedException();

    public Task<Asset?> GetRunAssetByName(Guid runId, string name) => throw new NotImplementedException();

    public Task<Asset?> GetLibraryAssetByFolderAndName(string folderName, string name) => throw new NotImplementedException();

    public Task<Asset?> GetLibraryAssetByName(string name) => throw new NotImplementedException();

    public Task UpsertAsset(Asset asset) => throw new NotImplementedException();

    public Task DeleteAsset(Guid assetId) => throw new NotImplementedException();

    public Task<AssetFolder> GetAssetFolder(Guid folderId) => throw new NotImplementedException();

    public Task<AssetFolder?> GetAssetFolderByName(string name) => throw new NotImplementedException();

    public Task<int> GetAssetFolderCount(string? search) => throw new NotImplementedException();

    public Task<List<AssetFolder>> GetAssetFolders(string? search, SortDirection sortDirection, int skip, int take) => throw new NotImplementedException();

    public Task<int> GetAssetFolderAssetCount(Guid folderId) => throw new NotImplementedException();

    public Task UpsertAssetFolder(AssetFolder folder) => throw new NotImplementedException();

    public Task DeleteAssetFolder(Guid folderId) => throw new NotImplementedException();
}
