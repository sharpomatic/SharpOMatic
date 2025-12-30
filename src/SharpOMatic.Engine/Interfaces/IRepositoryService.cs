namespace SharpOMatic.Engine.Interfaces;

public interface IRepositoryService
{
    // ------------------------------------------------
    // Workflow Operations
    // ------------------------------------------------
    Task<List<WorkflowEditSummary>> GetWorkflowEditSummaries();
    Task<WorkflowEntity> GetWorkflow(Guid workflowId);
    Task UpsertWorkflow(WorkflowEntity workflow);
    Task DeleteWorkflow(Guid workflowId);

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
    Task<Model> GetModel(Guid modelId);
    Task UpsertModel(Model model);
    Task DeleteModel(Guid modelId);

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
    Task<int> GetAssetCount(AssetScope scope, string? search);
    Task<List<Asset>> GetAssetsByScope(AssetScope scope, string? search, AssetSortField sortBy, SortDirection sortDirection, int skip, int take);
    Task<List<Asset>> GetRunAssets(Guid runId);
    Task UpsertAsset(Asset asset);
    Task DeleteAsset(Guid assetId);
}
