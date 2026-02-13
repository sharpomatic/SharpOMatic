namespace SharpOMatic.Engine.Interfaces;

public interface IRepositoryService
{
    // ------------------------------------------------
    // Workflow Operations
    // ------------------------------------------------
    Task<List<WorkflowSummary>> GetWorkflowSummaries();
    Task<int> GetWorkflowSummaryCount(string? search);
    Task<List<WorkflowSummary>> GetWorkflowSummaries(string? search, WorkflowSortField sortBy, SortDirection sortDirection, int skip, int take);
    Task<WorkflowEntity> GetWorkflow(Guid workflowId);
    Task UpsertWorkflow(WorkflowEntity workflow);
    Task DeleteWorkflow(Guid workflowId);
    Task<Guid> CopyWorkflow(Guid workflowId);

    // ------------------------------------------------
    // Run Operations
    // ------------------------------------------------
    Task<Run?> GetRun(Guid runId);
    Task<Run?> GetLatestRunForWorkflow(Guid workflowId);
    Task<int> GetWorkflowRunCount(Guid workflowId);
    Task<List<Run>> GetWorkflowRuns(Guid workflowId, RunSortField sortBy, SortDirection sortDirection, int skip, int take);
    Task UpsertRun(Run run);
    Task PruneWorkflowRuns(Guid workflowId, int keepLatest);

    // ------------------------------------------------
    // Trace Operations
    // ------------------------------------------------
    Task<List<Trace>> GetRunTraces(Guid runId);
    Task UpsertTrace(Trace trace);

    // ------------------------------------------------
    // ConnectorConfig Operations
    // ------------------------------------------------
    Task<List<ConnectorConfig>> GetConnectorConfigs();
    Task<ConnectorConfig?> GetConnectorConfig(string configId);
    Task UpsertConnectorConfig(ConnectorConfig config);

    // ------------------------------------------------
    // Connector Operations
    // ------------------------------------------------
    Task<List<ConnectorSummary>> GetConnectorSummaries();
    Task<int> GetConnectorSummaryCount(string? search);
    Task<List<ConnectorSummary>> GetConnectorSummaries(string? search, ConnectorSortField sortBy, SortDirection sortDirection, int skip, int take);
    Task<Connector> GetConnector(Guid connectionId, bool hideSecrets = true);
    Task UpsertConnector(Connector connection, bool hideSecrets = true);
    Task DeleteConnector(Guid connectionId);

    // ------------------------------------------------
    // ModelConfig Operations
    // ------------------------------------------------
    Task<List<ModelConfig>> GetModelConfigs();
    Task<ModelConfig?> GetModelConfig(string configId);
    Task UpsertModelConfig(ModelConfig config);

    // ------------------------------------------------
    // Model Operations
    // ------------------------------------------------
    Task<List<ModelSummary>> GetModelSummaries();
    Task<int> GetModelSummaryCount(string? search);
    Task<List<ModelSummary>> GetModelSummaries(string? search, ModelSortField sortBy, SortDirection sortDirection, int skip, int take);
    Task<Model> GetModel(Guid modelId, bool hideSecrets = true);
    Task UpsertModel(Model model);
    Task DeleteModel(Guid modelId);

    // ------------------------------------------------
    // EvalConfig Operations
    // ------------------------------------------------
    Task<int> GetEvalConfigSummaryCount(string? search);
    Task<List<EvalConfigSummary>> GetEvalConfigSummaries(string? search, EvalConfigSortField sortBy, SortDirection sortDirection, int skip, int take);
    Task<EvalConfig> GetEvalConfig(Guid evalConfigId);
    Task<EvalConfigDetail> GetEvalConfigDetail(Guid evalConfigId);
    Task<int> GetEvalRunSummaryCount(Guid evalConfigId, string? search);
    Task<List<EvalRunSummary>> GetEvalRunSummaries(Guid evalConfigId, string? search, EvalRunSortField sortBy, SortDirection sortDirection, int skip, int take);
    Task UpsertEvalRunGraderSummaries(List<EvalRunGraderSummary> graderSummaries);
    Task<EvalRun> GetEvalRun(Guid evalRunId);
    Task<EvalRunDetail> GetEvalRunDetail(Guid evalRunId);
    Task<List<EvalRunRowDetail>> GetEvalRunRows(Guid evalRunId, string? search, EvalRunRowSortField sortBy, SortDirection sortDirection, int skip, int take);
    Task<int> GetEvalRunRowCount(Guid evalRunId, string? search);
    Task<List<EvalRunRowGrader>> GetEvalRunRowGraders(Guid evalRunId, Guid evalGraderId);
    Task UpsertEvalConfig(EvalConfig evalConfig);
    Task DeleteEvalConfig(Guid evalConfigId);
    Task UpsertEvalGraders(List<EvalGrader> graders);
    Task DeleteEvalGrader(Guid evalGraderId);
    Task UpsertEvalColumns(List<EvalColumn> columns);
    Task DeleteEvalColumn(Guid evalColumnId);
    Task UpsertEvalRows(List<EvalRow> rows);
    Task DeleteEvalRow(Guid evalRowId);
    Task UpsertEvalData(List<EvalData> data);
    Task DeleteEvalData(Guid evalDataId);
    Task UpsertEvalRun(EvalRun evalRun);
    Task UpsertEvalRunRows(List<EvalRunRow> runRows);
    Task UpsertEvalRunRowGraders(List<EvalRunRowGrader> runRowGraders);

    // ------------------------------------------------
    // Setting Operations
    // ------------------------------------------------
    Task<List<Setting>> GetSettings();
    Task<Setting?> GetSetting(string name);
    Task UpsertSetting(Setting model);

    // ------------------------------------------------
    // Asset Operations
    // ------------------------------------------------
    Task<Asset> GetAsset(Guid assetId);
    Task<int> GetAssetCount(AssetScope scope, string? search, Guid? runId = null);
    Task<List<Asset>> GetAssetsByScope(AssetScope scope, string? search, AssetSortField sortBy, SortDirection sortDirection, int skip, int take, Guid? runId = null);
    Task<List<Asset>> GetRunAssets(Guid runId);
    Task<Asset?> GetRunAssetByName(Guid runId, string name);
    Task<Asset?> GetLibraryAssetByName(string name);
    Task UpsertAsset(Asset asset);
    Task DeleteAsset(Guid assetId);
}
