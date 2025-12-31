using Microsoft.CodeAnalysis;
using SharpOMatic.Engine.Entities.Definitions;
using System.Threading.Tasks;

namespace SharpOMatic.Engine.Services;

public class RepositoryService(IDbContextFactory<SharpOMaticDbContext> dbContextFactory, IAssetStore? assetStore = null) : IRepositoryService
{
    private const string SECRET_OBFUSCATION = "********";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new NodeEntityConverter() }
    };

    // ------------------------------------------------
    // Workflow Operations
    // ------------------------------------------------
    public async Task<List<WorkflowEditSummary>> GetWorkflowEditSummaries()
    {
        return await GetWorkflowEditSummaries(null, WorkflowSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetWorkflowEditSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var workflows = ApplyWorkflowSearch(dbContext.Workflows.AsNoTracking(), search);
        return await workflows.CountAsync();
    }

    public async Task<List<WorkflowEditSummary>> GetWorkflowEditSummaries(
        string? search,
        WorkflowSortField sortBy,
        SortDirection sortDirection,
        int skip,
        int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflows = ApplyWorkflowSearch(dbContext.Workflows.AsNoTracking(), search);
        var sorted = GetSortedWorkflows(workflows, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted.Select(workflow => new WorkflowEditSummary
        {
            Version = workflow.Version,
            Id = workflow.WorkflowId,
            Name = workflow.Named,
            Description = workflow.Description,
        }).ToListAsync();
    }

    public async Task<WorkflowEntity> GetWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflow = await (from w in dbContext.Workflows
                              where w.WorkflowId == workflowId
                              select w).AsNoTracking().FirstOrDefaultAsync();

        return workflow is null
            ? throw new SharpOMaticException($"Workflow '{workflowId}' cannot be found.")
            : new WorkflowEntity()
            {
                Version = workflow.Version,
                Id = workflow.WorkflowId,
                Name = workflow.Named,
                Description = workflow.Description,
                Nodes = JsonSerializer.Deserialize<NodeEntity[]>(workflow.Nodes, _options)!,
                Connections = JsonSerializer.Deserialize<ConnectionEntity[]>(workflow.Connections, _options)!,
            };
    }

    public async Task UpsertWorkflow(WorkflowEntity workflow)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entry = await (from w in dbContext.Workflows
                           where w.WorkflowId == workflow.Id
                           select w).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new Workflow()
            {
                Version = workflow.Version,
                WorkflowId = workflow.Id,
                Named = "",
                Description = "",
                Nodes = "",
                Connections = ""
            };

            dbContext.Workflows.Add(entry);
        }

        entry.Named = workflow.Name;
        entry.Description = workflow.Description;
        entry.Nodes = JsonSerializer.Serialize(workflow.Nodes, _options);
        entry.Connections = JsonSerializer.Serialize(workflow.Connections, _options);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflow = await (from w in dbContext.Workflows
                              where w.WorkflowId == workflowId
                              select w).FirstOrDefaultAsync();

        if (workflow is null)
            throw new SharpOMaticException($"Workflow '{workflowId}' cannot be found.");

        var runIds = await dbContext.Runs
            .Where(r => r.WorkflowId == workflowId)
            .Select(r => r.RunId)
            .ToListAsync();

        await DeleteAssetStorageForRuns(runIds);

        dbContext.Remove(workflow);
        await dbContext.SaveChangesAsync();
    }

    private static IQueryable<Workflow> ApplyWorkflowSearch(IQueryable<Workflow> workflows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return workflows;

        var normalizedSearch = search.Trim().ToLower();
        return workflows.Where(workflow =>
            workflow.Named.ToLower().Contains(normalizedSearch) ||
            workflow.Description.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<Workflow> GetSortedWorkflows(
        IQueryable<Workflow> workflows,
        WorkflowSortField sortBy,
        SortDirection sortDirection)
    {
        return sortBy switch
        {
            WorkflowSortField.Description => sortDirection == SortDirection.Ascending
                ? workflows.OrderBy(workflow => workflow.Description).ThenBy(workflow => workflow.Named)
                : workflows.OrderByDescending(workflow => workflow.Description).ThenByDescending(workflow => workflow.Named),
            _ => sortDirection == SortDirection.Ascending
                ? workflows.OrderBy(workflow => workflow.Named).ThenBy(workflow => workflow.Description)
                : workflows.OrderByDescending(workflow => workflow.Named).ThenByDescending(workflow => workflow.Description),
        };
    }

    // ------------------------------------------------
    // Run Operations
    // ------------------------------------------------
    public async Task<Run?> GetRun(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.RunId == runId);
    }

    public async Task<Run?> GetLatestRunForWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.Runs.AsNoTracking()
            .Where(r => r.WorkflowId == workflowId)
            .OrderByDescending(r => r.Created)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetWorkflowRunCount(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.Runs.AsNoTracking()
            .Where(r => r.WorkflowId == workflowId)
            .CountAsync();
    }

    public async Task<List<Run>> GetWorkflowRuns(Guid workflowId, RunSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var runs = dbContext.Runs.AsNoTracking()
            .Where(r => r.WorkflowId == workflowId);

        var sortedRuns = GetSortedRuns(runs, sortBy, sortDirection);

        if (skip > 0)
            sortedRuns = sortedRuns.Skip(skip);

        if (take > 0)
            sortedRuns = sortedRuns.Take(take);

        return await sortedRuns.ToListAsync();
    }

    public async Task UpsertRun(Run run)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from r in dbContext.Runs
                            where r.RunId == run.RunId
                            select r).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.Runs.Add(run);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(run);

        await dbContext.SaveChangesAsync();
    }

    public async Task PruneWorkflowRuns(Guid workflowId, int keepLatest)
    {
        if (keepLatest < 0)
            keepLatest = 0;

        using var dbContext = dbContextFactory.CreateDbContext();

        var runIdsToDelete = await dbContext.Runs
            .Where(r => r.WorkflowId == workflowId)
            .OrderByDescending(r => r.Created)
            .Skip(keepLatest)
            .Select(r => r.RunId)
            .ToListAsync();

        if (runIdsToDelete.Count == 0)
            return;

        await DeleteAssetStorageForRuns(runIdsToDelete);

        await dbContext.Runs
            .Where(r => runIdsToDelete.Contains(r.RunId))
            .ExecuteDeleteAsync();
    }

    private static IQueryable<Run> GetSortedRuns(IQueryable<Run> runs, RunSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            RunSortField.Status => sortDirection == SortDirection.Ascending
                ? runs.OrderBy(r => r.RunStatus).ThenByDescending(r => r.Created)
                : runs.OrderByDescending(r => r.RunStatus).ThenByDescending(r => r.Created),
            _ => sortDirection == SortDirection.Ascending
                ? runs.OrderBy(r => r.Created)
                : runs.OrderByDescending(r => r.Created),
        };
    }

    // ------------------------------------------------
    // Trace Operations
    // ------------------------------------------------
    public async Task<List<Trace>> GetRunTraces(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from t in dbContext.Traces.AsNoTracking()
                      where t.RunId == runId
                      orderby t.Created
                      select t).ToListAsync();
    }

    public async Task UpsertTrace(Trace trace)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from t in dbContext.Traces
                            where t.TraceId == trace.TraceId
                            select t).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.Traces.Add(trace);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(trace);

        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // ConnectorConfig Operations
    // ------------------------------------------------
    public async Task<ConnectorConfig?> GetConnectorConfig(string configId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorConfigMetadata
                              where c.ConfigId == configId
                              select c).AsNoTracking().FirstOrDefaultAsync();

        if (metadata is null)
            return null;

        return JsonSerializer.Deserialize<ConnectorConfig>(metadata.Config, _options);

    }

    public async Task<List<ConnectorConfig>> GetConnectorConfigs()
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entries = await dbContext.ConnectorConfigMetadata.AsNoTracking().ToListAsync();

        var results = new List<ConnectorConfig>();
        foreach (var entry in entries)
        {
            var config = JsonSerializer.Deserialize<ConnectorConfig>(entry.Config, _options);
            if (config != null)
                results.Add(config);
        }

        return results;
    }

    public async Task UpsertConnectorConfig(ConnectorConfig config)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorConfigMetadata
                              where c.ConfigId == config.ConfigId
                              select c).FirstOrDefaultAsync();

        if (metadata is null)
        {
            metadata = new ConnectorConfigMetadata()
            {
                Version = config.Version,
                ConfigId = config.ConfigId,
                Config = ""
            };

            dbContext.ConnectorConfigMetadata.Add(metadata);
        }

        metadata.Config = JsonSerializer.Serialize(config, _options);
        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Connector Operations
    // ------------------------------------------------
    public async Task<List<ConnectorSummary>> GetConnectorSummaries()
    {
        return await GetConnectorSummaries(null, ConnectorSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetConnectorSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var connectors = ApplyConnectorSearch(dbContext.ConnectorMetadata.AsNoTracking(), search);
        return await connectors.CountAsync();
    }

    public async Task<List<ConnectorSummary>> GetConnectorSummaries(
        string? search,
        ConnectorSortField sortBy,
        SortDirection sortDirection,
        int skip,
        int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var connectors = ApplyConnectorSearch(dbContext.ConnectorMetadata.AsNoTracking(), search);
        var sorted = GetSortedConnectors(connectors, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted.Select(connector => new ConnectorSummary
        {
            ConnectorId = connector.ConnectorId,
            Name = connector.Name,
            Description = connector.Description,
        }).ToListAsync();
    }

    public async Task<Connector> GetConnector(Guid connectorId, bool hideSecrets = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorMetadata
                              where c.ConnectorId == connectorId
                              select c).AsNoTracking().FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Connector '{connectorId}' cannot be found.");

        var connector = JsonSerializer.Deserialize<Connector>(metadata.Config);

        if (connector is null)
            throw new SharpOMaticException($"Connector '{connectorId}' configuration is invalid.");

        // We need to ensure that any field that is a secret, is replaced to prevent it being available to clients
        if (hideSecrets && (connector.FieldValues.Count > 0) && !string.IsNullOrWhiteSpace(connector.ConfigId))
        {
            var config = await GetConnectorConfig(connector.ConfigId);
            if (config is not null)
            {
                foreach (var authModes in config.AuthModes)
                {
                    foreach (var field in authModes.Fields)
                        if ((field.Type == FieldDescriptorType.Secret) && connector.FieldValues.ContainsKey(field.Name))
                            connector.FieldValues[field.Name] = SECRET_OBFUSCATION;
                }
            }
        }

        return connector;
    }

    public async Task UpsertConnector(Connector connector, bool hideSecrets = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entry = await (from c in dbContext.ConnectorMetadata
                           where c.ConnectorId == connector.ConnectorId
                           select c).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new ConnectorMetadata()
            {
                ConnectorId = connector.ConnectorId,
                Version = connector.Version,
                Name = "",
                Description = "",
                Config = ""
            };

            dbContext.ConnectorMetadata.Add(entry);
        }
        else if (hideSecrets)
        {
            // If any provided secrets are the obfuscated value then we do not want to overwrite the existing value
            var entryConfig = JsonSerializer.Deserialize<Connector>(entry.Config);
            if (entryConfig is not null)
            {
                var config = await GetConnectorConfig(connector.ConfigId);
                if (config is not null)
                {
                    foreach (var authModes in config.AuthModes)
                    {
                        foreach (var field in authModes.Fields)
                        {
                            if ((field.Type == FieldDescriptorType.Secret) &&
                                connector.FieldValues.ContainsKey(field.Name) &&
                                entryConfig.FieldValues.ContainsKey(field.Name) &&
                                (connector.FieldValues[field.Name] == SECRET_OBFUSCATION))
                            {
                                connector.FieldValues[field.Name] = entryConfig.FieldValues[field.Name];
                            }
                        }
                    }
                }
            }
        }

        entry.Name = connector.Name;
        entry.Description = connector.Description;
        entry.Config = JsonSerializer.Serialize(connector);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteConnector(Guid connectorId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorMetadata
                              where c.ConnectorId == connectorId
                              select c).FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Connector '{connectorId}' cannot be found.");

        dbContext.Remove(metadata);
        await dbContext.SaveChangesAsync();
    }

    private static IQueryable<ConnectorMetadata> ApplyConnectorSearch(IQueryable<ConnectorMetadata> connectors, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return connectors;

        var normalizedSearch = search.Trim().ToLower();
        return connectors.Where(connector =>
            connector.Name.ToLower().Contains(normalizedSearch) ||
            connector.Description.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<ConnectorMetadata> GetSortedConnectors(
        IQueryable<ConnectorMetadata> connectors,
        ConnectorSortField sortBy,
        SortDirection sortDirection)
    {
        return sortBy switch
        {
            ConnectorSortField.Description => sortDirection == SortDirection.Ascending
                ? connectors.OrderBy(connector => connector.Description).ThenBy(connector => connector.Name)
                : connectors.OrderByDescending(connector => connector.Description).ThenByDescending(connector => connector.Name),
            _ => sortDirection == SortDirection.Ascending
                ? connectors.OrderBy(connector => connector.Name).ThenBy(connector => connector.Description)
                : connectors.OrderByDescending(connector => connector.Name).ThenByDescending(connector => connector.Description),
        };
    }

    // ------------------------------------------------
    // ModelConfig Operations
    // ------------------------------------------------
    public async Task<ModelConfig?> GetModelConfig(string configId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelConfigMetadata
                              where m.ConfigId == configId
                              select m).AsNoTracking().FirstOrDefaultAsync();

        if (metadata is null)
            return null;

        return JsonSerializer.Deserialize<ModelConfig>(metadata.Config, _options);

    }

    public async Task<List<ModelConfig>> GetModelConfigs()
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entries = await dbContext.ModelConfigMetadata.AsNoTracking().ToListAsync();

        var results = new List<ModelConfig>();
        foreach (var entry in entries)
        {
            var config = JsonSerializer.Deserialize<ModelConfig>(entry.Config, _options);
            if (config != null)
                results.Add(config);
        }

        return results;
    }

    public async Task UpsertModelConfig(ModelConfig config)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelConfigMetadata
                              where m.ConfigId == config.ConfigId
                              select m).FirstOrDefaultAsync();

        if (metadata is null)
        {
            metadata = new ModelConfigMetadata()
            {
                Version = config.Version,
                ConfigId = config.ConfigId,
                Config = ""
            };

            dbContext.ModelConfigMetadata.Add(metadata);
        }

        metadata.Config = JsonSerializer.Serialize(config, _options);
        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Model Operations
    // ------------------------------------------------
    public async Task<List<ModelSummary>> GetModelSummaries()
    {
        return await GetModelSummaries(null, ModelSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetModelSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var models = ApplyModelSearch(dbContext.ModelMetadata.AsNoTracking(), search);
        return await models.CountAsync();
    }

    public async Task<List<ModelSummary>> GetModelSummaries(
        string? search,
        ModelSortField sortBy,
        SortDirection sortDirection,
        int skip,
        int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var models = ApplyModelSearch(dbContext.ModelMetadata.AsNoTracking(), search);
        var sorted = GetSortedModels(models, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted.Select(model => new ModelSummary
        {
            ModelId = model.ModelId,
            Name = model.Name,
            Description = model.Description,
        }).ToListAsync();
    }

    public async Task<Model> GetModel(Guid modelId, bool hideSecrets = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelMetadata
                              where m.ModelId == modelId
                              select m).AsNoTracking().FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Model '{modelId}' cannot be found.");

        var model = JsonSerializer.Deserialize<Model>(metadata.Config);

        if (model is null)
            throw new SharpOMaticException($"Model '{modelId}' configuration is invalid.");

        // We need to ensure that any parameter that is a secret, is replaced to prevent it being available in the client
        if (hideSecrets && (model.ParameterValues.Count > 0) && !string.IsNullOrWhiteSpace(model.ConfigId))
        {
            var config = await GetModelConfig(model.ConfigId);
            if (config is not null)
            {
                foreach (var field in config.ParameterFields)
                    if ((field.Type == FieldDescriptorType.Secret) && model.ParameterValues.ContainsKey(field.Name))
                        model.ParameterValues[field.Name] = "**********";
            }
        }

        return model;
    }

    public async Task UpsertModel(Model model)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entry = await (from m in dbContext.ModelMetadata
                           where m.ModelId == model.ModelId
                           select m).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new ModelMetadata()
            {
                ModelId = model.ModelId,
                Version = model.Version,
                Name = "",
                Description = "",
                Config = ""
            };

            dbContext.ModelMetadata.Add(entry);
        }

        entry.Name = model.Name;
        entry.Description = model.Description;
        entry.Config = JsonSerializer.Serialize(model);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteModel(Guid modelId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelMetadata
                              where m.ModelId == modelId
                              select m).FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Model '{modelId}' cannot be found.");

        dbContext.Remove(metadata);
        await dbContext.SaveChangesAsync();
    }

    private static IQueryable<ModelMetadata> ApplyModelSearch(IQueryable<ModelMetadata> models, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return models;

        var normalizedSearch = search.Trim().ToLower();
        return models.Where(model =>
            model.Name.ToLower().Contains(normalizedSearch) ||
            model.Description.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<ModelMetadata> GetSortedModels(
        IQueryable<ModelMetadata> models,
        ModelSortField sortBy,
        SortDirection sortDirection)
    {
        return sortBy switch
        {
            ModelSortField.Description => sortDirection == SortDirection.Ascending
                ? models.OrderBy(model => model.Description).ThenBy(model => model.Name)
                : models.OrderByDescending(model => model.Description).ThenByDescending(model => model.Name),
            _ => sortDirection == SortDirection.Ascending
                ? models.OrderBy(model => model.Name).ThenBy(model => model.Description)
                : models.OrderByDescending(model => model.Name).ThenByDescending(model => model.Description),
        };
    }

    // ------------------------------------------------
    // Setting Operations
    // ------------------------------------------------

    public async Task<List<Setting>> GetSettings()
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from s in dbContext.Settings.AsNoTracking()
                      orderby s.Name
                      select s).ToListAsync();
    }

    public async Task<Setting?> GetSetting(string name)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from s in dbContext.Settings.AsNoTracking()
                      where s.Name == name
                      select s).FirstOrDefaultAsync();
    }

    public async Task UpsertSetting(Setting model)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var setting = await (from s in dbContext.Settings
                             where s.Name == model.Name
                             select s).FirstOrDefaultAsync();

        if (setting is null)
            dbContext.Settings.Add(model);
        else
            dbContext.Entry(setting).CurrentValues.SetValues(model);

        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Asset Operations
    // ------------------------------------------------
    public async Task<Asset> GetAsset(Guid assetId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var asset = await dbContext.Assets.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssetId == assetId);

        if (asset is null)
            throw new SharpOMaticException($"Asset '{assetId}' cannot be found.");

        return asset;
    }

    public async Task<int> GetAssetCount(AssetScope scope, string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var assets = dbContext.Assets.AsNoTracking()
            .Where(a => a.Scope == scope);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var searchLower = normalizedSearch.ToLower();
            assets = assets.Where(a => a.Name.ToLower().Contains(searchLower));
        }

        return await assets.CountAsync();
    }

    public async Task<List<Asset>> GetAssetsByScope(AssetScope scope, string? search, AssetSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var assets = dbContext.Assets.AsNoTracking()
            .Where(a => a.Scope == scope);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var searchLower = normalizedSearch.ToLower();
            assets = assets.Where(a => a.Name.ToLower().Contains(searchLower));
        }

        var sortedAssets = GetSortedAssets(assets, sortBy, sortDirection);

        if (skip > 0)
            sortedAssets = sortedAssets.Skip(skip);

        if (take > 0)
            sortedAssets = sortedAssets.Take(take);

        return await sortedAssets.ToListAsync();
    }

    private static IQueryable<Asset> GetSortedAssets(IQueryable<Asset> assets, AssetSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            AssetSortField.Name => sortDirection == SortDirection.Ascending
                ? assets.OrderBy(a => a.Name).ThenByDescending(a => a.Created)
                : assets.OrderByDescending(a => a.Name).ThenByDescending(a => a.Created),
            AssetSortField.Type => sortDirection == SortDirection.Ascending
                ? assets.OrderBy(a => a.MediaType).ThenByDescending(a => a.Created)
                : assets.OrderByDescending(a => a.MediaType).ThenByDescending(a => a.Created),
            AssetSortField.Size => sortDirection == SortDirection.Ascending
                ? assets.OrderBy(a => a.SizeBytes).ThenByDescending(a => a.Created)
                : assets.OrderByDescending(a => a.SizeBytes).ThenByDescending(a => a.Created),
            AssetSortField.Created => sortDirection == SortDirection.Ascending
                ? assets.OrderBy(a => a.Created).ThenBy(a => a.Name)
                : assets.OrderByDescending(a => a.Created).ThenBy(a => a.Name),
            _ => sortDirection == SortDirection.Ascending
                ? assets.OrderBy(a => a.Name).ThenByDescending(a => a.Created)
                : assets.OrderByDescending(a => a.Name).ThenByDescending(a => a.Created),
        };
    }

    public async Task<List<Asset>> GetRunAssets(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.Assets.AsNoTracking()
            .Where(a => a.RunId == runId)
            .OrderByDescending(a => a.Created)
            .ToListAsync();
    }

    public async Task UpsertAsset(Asset asset)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from a in dbContext.Assets
                            where a.AssetId == asset.AssetId
                            select a).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.Assets.Add(asset);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(asset);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsset(Guid assetId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var asset = await (from a in dbContext.Assets
                           where a.AssetId == assetId
                           select a).FirstOrDefaultAsync();

        if (asset is null)
            throw new SharpOMaticException($"Asset '{assetId}' cannot be found.");

        dbContext.Remove(asset);
        await dbContext.SaveChangesAsync();
    }

    private async Task DeleteAssetStorageForRuns(IReadOnlyCollection<Guid> runIds)
    {
        if (assetStore is null || runIds.Count == 0)
            return;

        using var dbContext = dbContextFactory.CreateDbContext();

        var storageKeys = await dbContext.Assets.AsNoTracking()
            .Where(a => a.RunId.HasValue && runIds.Contains(a.RunId.Value))
            .Select(a => a.StorageKey)
            .ToListAsync();

        foreach (var storageKey in storageKeys)
            await assetStore.DeleteAsync(storageKey);
    }
}
