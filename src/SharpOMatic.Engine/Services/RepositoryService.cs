using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SharpOMatic.Engine.Entities.Definitions;
using SharpOMatic.Engine.Repository;

namespace SharpOMatic.Engine.Services;

public class RepositoryService(IDbContextFactory<SharpOMaticDbContext> dbContextFactory, IAssetStore? assetStore = null) : IRepositoryService
{
    private const string SECRET_OBFUSCATION = "********";

    private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new NodeEntityConverter() } };

    // ------------------------------------------------
    // Workflow Operations
    // ------------------------------------------------
    public async Task<List<WorkflowSummary>> GetWorkflowSummaries()
    {
        return await GetWorkflowSummaries(null, WorkflowSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetWorkflowSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var workflows = ApplyWorkflowSearch(dbContext.Workflows.AsNoTracking(), search);
        return await workflows.CountAsync();
    }

    public async Task<List<WorkflowSummary>> GetWorkflowSummaries(string? search, WorkflowSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflows = ApplyWorkflowSearch(dbContext.Workflows.AsNoTracking(), search);
        var sorted = GetSortedWorkflows(workflows, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(workflow => new WorkflowSummary
            {
                Version = workflow.Version,
                Id = workflow.WorkflowId,
                Name = workflow.Named,
                Description = workflow.Description,
            })
            .ToListAsync();
    }

    public async Task<WorkflowEntity> GetWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflow = await (from w in dbContext.Workflows where w.WorkflowId == workflowId select w)
            .AsNoTracking()
            .FirstOrDefaultAsync();

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

        var entry = await (from w in dbContext.Workflows where w.WorkflowId == workflow.Id select w).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new Workflow()
            {
                Version = workflow.Version,
                WorkflowId = workflow.Id,
                Named = "",
                Description = "",
                Nodes = "",
                Connections = "",
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

        var workflow = await (from w in dbContext.Workflows where w.WorkflowId == workflowId select w).FirstOrDefaultAsync();

        if (workflow is null)
            throw new SharpOMaticException($"Workflow '{workflowId}' cannot be found.");

        var runIds = await dbContext.Runs.Where(r => r.WorkflowId == workflowId).Select(r => r.RunId).ToListAsync();

        await DeleteAssetStorageForRuns(runIds);

        dbContext.Remove(workflow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Guid> CopyWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflow = await dbContext.Workflows.AsNoTracking().FirstOrDefaultAsync(w => w.WorkflowId == workflowId);

        if (workflow is null)
            throw new SharpOMaticException($"Workflow '{workflowId}' cannot be found.");

        var existingNames = await dbContext.Workflows.AsNoTracking().Select(w => w.Named).ToListAsync();

        var nameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var baseName = workflow.Named;
        var startIndex = 1;

        if (TryParseCopySuffix(baseName, out var parsedBaseName, out var copyNumber))
        {
            baseName = parsedBaseName;
            startIndex = Math.Max(copyNumber + 1, 2);
        }

        var copyName = string.Empty;

        if (startIndex == 1)
        {
            copyName = $"{baseName} (Copy)";
            if (nameSet.Contains(copyName))
                startIndex = 2;
        }

        if (startIndex > 1)
        {
            for (var i = startIndex; ; i++)
            {
                copyName = $"{baseName} (Copy {i})";
                if (!nameSet.Contains(copyName))
                    break;
            }
        }

        var newWorkflow = new Workflow
        {
            WorkflowId = Guid.NewGuid(),
            Version = workflow.Version,
            Named = copyName,
            Description = workflow.Description,
            Nodes = workflow.Nodes,
            Connections = workflow.Connections,
        };

        dbContext.Workflows.Add(newWorkflow);
        await dbContext.SaveChangesAsync();

        return newWorkflow.WorkflowId;
    }

    private static bool TryParseCopySuffix(string name, out string baseName, out int copyNumber)
    {
        baseName = name;
        copyNumber = 0;

        const string copySuffix = " (Copy)";
        if (name.EndsWith(copySuffix, StringComparison.OrdinalIgnoreCase))
        {
            baseName = name.Substring(0, name.Length - copySuffix.Length);
            copyNumber = 1;
            return true;
        }

        const string copyPrefix = " (Copy ";
        if (!name.EndsWith(")", StringComparison.OrdinalIgnoreCase))
            return false;

        var prefixIndex = name.LastIndexOf(copyPrefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0)
            return false;

        var numberStart = prefixIndex + copyPrefix.Length;
        var numberLength = name.Length - numberStart - 1;
        if (numberLength <= 0)
            return false;

        var numberSpan = name.AsSpan(numberStart, numberLength);
        if (!int.TryParse(numberSpan, out var parsed) || parsed < 1)
            return false;

        baseName = name.Substring(0, prefixIndex);
        copyNumber = parsed;
        return true;
    }

    private static IQueryable<Workflow> ApplyWorkflowSearch(IQueryable<Workflow> workflows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return workflows;

        var normalizedSearch = search.Trim().ToLower();
        return workflows.Where(workflow => workflow.Named.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<Workflow> GetSortedWorkflows(IQueryable<Workflow> workflows, WorkflowSortField sortBy, SortDirection sortDirection)
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

        return await dbContext.Runs.AsNoTracking().Where(r => r.WorkflowId == workflowId).OrderByDescending(r => r.Created).FirstOrDefaultAsync();
    }

    public async Task<int> GetWorkflowRunCount(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.Runs.AsNoTracking().Where(r => r.WorkflowId == workflowId).CountAsync();
    }

    public async Task<List<Run>> GetWorkflowRuns(Guid workflowId, RunSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var runs = dbContext.Runs.AsNoTracking().Where(r => r.WorkflowId == workflowId);

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

        var entity = await (from r in dbContext.Runs where r.RunId == run.RunId select r).FirstOrDefaultAsync();

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

        var runIdsToDelete = await dbContext.Runs.Where(r => r.WorkflowId == workflowId).OrderByDescending(r => r.Created).Skip(keepLatest).Select(r => r.RunId).ToListAsync();

        if (runIdsToDelete.Count == 0)
            return;

        await DeleteAssetStorageForRuns(runIdsToDelete);

        await dbContext.Runs.Where(r => runIdsToDelete.Contains(r.RunId)).ExecuteDeleteAsync();
    }

    private static IQueryable<Run> GetSortedRuns(IQueryable<Run> runs, RunSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            RunSortField.Status => sortDirection == SortDirection.Ascending
                ? runs.OrderBy(r => r.RunStatus).ThenByDescending(r => r.Created)
                : runs.OrderByDescending(r => r.RunStatus).ThenByDescending(r => r.Created),
            _ => sortDirection == SortDirection.Ascending ? runs.OrderBy(r => r.Created) : runs.OrderByDescending(r => r.Created),
        };
    }

    // ------------------------------------------------
    // Trace Operations
    // ------------------------------------------------
    public async Task<List<Trace>> GetRunTraces(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from t in dbContext.Traces.AsNoTracking() where t.RunId == runId orderby t.Created select t).ToListAsync();
    }

    public async Task UpsertTrace(Trace trace)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from t in dbContext.Traces where t.TraceId == trace.TraceId select t).FirstOrDefaultAsync();

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

        var metadata = await (from c in dbContext.ConnectorConfigMetadata where c.ConfigId == configId select c)
            .AsNoTracking()
            .FirstOrDefaultAsync();

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

        var metadata = await (from c in dbContext.ConnectorConfigMetadata where c.ConfigId == config.ConfigId select c).FirstOrDefaultAsync();

        if (metadata is null)
        {
            metadata = new ConnectorConfigMetadata()
            {
                Version = config.Version,
                ConfigId = config.ConfigId,
                Config = "",
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

    public async Task<List<ConnectorSummary>> GetConnectorSummaries(string? search, ConnectorSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var connectors = ApplyConnectorSearch(dbContext.ConnectorMetadata.AsNoTracking(), search);
        var sorted = GetSortedConnectors(connectors, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(connector => new ConnectorSummary
            {
                ConnectorId = connector.ConnectorId,
                Name = connector.Name,
                Description = connector.Description,
            })
            .ToListAsync();
    }

    public async Task<Connector> GetConnector(Guid connectorId, bool hideSecrets = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorMetadata where c.ConnectorId == connectorId select c)
            .AsNoTracking()
            .FirstOrDefaultAsync();

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

        var entry = await (from c in dbContext.ConnectorMetadata where c.ConnectorId == connector.ConnectorId select c).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new ConnectorMetadata()
            {
                ConnectorId = connector.ConnectorId,
                Version = connector.Version,
                Name = "",
                Description = "",
                Config = "",
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
                            if (
                                (field.Type == FieldDescriptorType.Secret)
                                && connector.FieldValues.ContainsKey(field.Name)
                                && entryConfig.FieldValues.ContainsKey(field.Name)
                                && (connector.FieldValues[field.Name] == SECRET_OBFUSCATION)
                            )
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

        var metadata = await (from c in dbContext.ConnectorMetadata where c.ConnectorId == connectorId select c).FirstOrDefaultAsync();

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
        return connectors.Where(connector => connector.Name.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<ConnectorMetadata> GetSortedConnectors(IQueryable<ConnectorMetadata> connectors, ConnectorSortField sortBy, SortDirection sortDirection)
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

        var metadata = await (from m in dbContext.ModelConfigMetadata where m.ConfigId == configId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

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

        var metadata = await (from m in dbContext.ModelConfigMetadata where m.ConfigId == config.ConfigId select m).FirstOrDefaultAsync();

        if (metadata is null)
        {
            metadata = new ModelConfigMetadata()
            {
                Version = config.Version,
                ConfigId = config.ConfigId,
                Config = "",
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

    public async Task<List<ModelSummary>> GetModelSummaries(string? search, ModelSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var models = ApplyModelSearch(dbContext.ModelMetadata.AsNoTracking(), search);
        var sorted = GetSortedModels(models, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(model => new ModelSummary
            {
                ModelId = model.ModelId,
                Name = model.Name,
                Description = model.Description,
            })
            .ToListAsync();
    }

    public async Task<Model> GetModel(Guid modelId, bool hideSecrets = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelMetadata where m.ModelId == modelId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

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

        var entry = await (from m in dbContext.ModelMetadata where m.ModelId == model.ModelId select m).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new ModelMetadata()
            {
                ModelId = model.ModelId,
                Version = model.Version,
                Name = "",
                Description = "",
                Config = "",
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

        var metadata = await (from m in dbContext.ModelMetadata where m.ModelId == modelId select m).FirstOrDefaultAsync();

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
        return models.Where(model => model.Name.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<ModelMetadata> GetSortedModels(IQueryable<ModelMetadata> models, ModelSortField sortBy, SortDirection sortDirection)
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
    // EvalConfig Operations
    // ------------------------------------------------
    public async Task<List<EvalConfigSummary>> GetEvalConfigSummaries()
    {
        return await GetEvalConfigSummaries(null, EvalConfigSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetEvalConfigSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var evalConfigs = ApplyModelSearch(dbContext.EvalConfigs.AsNoTracking(), search);
        return await evalConfigs.CountAsync();
    }

    public async Task<List<EvalConfigSummary>> GetEvalConfigSummaries(string? search, EvalConfigSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfigs = ApplyModelSearch(dbContext.EvalConfigs.AsNoTracking(), search);
        var sorted = GetSortedModels(evalConfigs, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(config => new EvalConfigSummary
            {
                EvalConfigId = config.EvalConfigId,
                Name = config.Name,
                Description = config.Description,
            })
            .ToListAsync();
    }

    public async Task<EvalConfig> GetEvalConfig(Guid evalConfigId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfig = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfigId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (evalConfig is null)
            throw new SharpOMaticException($"EvalConfig '{evalConfigId}' cannot be found.");

        return evalConfig;
    }

    public async Task<EvalConfigDetail> GetEvalConfigDetail(Guid evalConfigId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfig = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfigId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (evalConfig is null)
            throw new SharpOMaticException($"EvalConfig '{evalConfigId}' cannot be found.");

        var graders = await (from g in dbContext.EvalGraders.AsNoTracking() where g.EvalConfigId == evalConfigId orderby g.Order select g).ToListAsync();

        var columns = await (from c in dbContext.EvalColumns.AsNoTracking() where c.EvalConfigId == evalConfigId orderby c.Order select c).ToListAsync();

        var rows = await (from r in dbContext.EvalRows.AsNoTracking() where r.EvalConfigId == evalConfigId orderby r.Order select r).ToListAsync();

        var data = await (
            from d in dbContext.EvalData.AsNoTracking()
            join r in dbContext.EvalRows.AsNoTracking() on d.EvalRowId equals r.EvalRowId
            join c in dbContext.EvalColumns.AsNoTracking() on d.EvalColumnId equals c.EvalColumnId
            where r.EvalConfigId == evalConfigId && c.EvalConfigId == evalConfigId
            orderby r.Order, c.Order
            select d
        ).ToListAsync();

        return new EvalConfigDetail
        {
            EvalConfig = evalConfig,
            Graders = graders,
            Columns = columns,
            Rows = rows,
            Data = data,
        };
    }

    public async Task UpsertEvalConfig(EvalConfig evalConfig)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfig.EvalConfigId select m).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.EvalConfigs.Add(evalConfig);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(evalConfig);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalConfig(Guid evalConfigId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfig = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfigId select m).FirstOrDefaultAsync();

        if (evalConfig is null)
            throw new SharpOMaticException($"EvalConfig '{evalConfigId}' cannot be found.");

        dbContext.Remove(evalConfig);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalGraders(List<EvalGrader> graders)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (graders.Count == 0)
            return;

        var ids = graders.Select(grader => grader.EvalGraderId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalGraders where ids.Contains(run.EvalGraderId) select run).ToListAsync();

        foreach (var grader in graders)
        {
            var entity = entities.Where(e => e.EvalGraderId == grader.EvalGraderId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalGraders.Add(grader);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(grader);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalGrader(Guid evalGraderId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var grader = await (from g in dbContext.EvalGraders where g.EvalGraderId == evalGraderId select g).FirstOrDefaultAsync();

        if (grader is null)
            throw new SharpOMaticException($"EvalGrader '{evalGraderId}' cannot be found.");

        await (from g in dbContext.EvalRunRowGraders where g.EvalGraderId == evalGraderId select g).ExecuteDeleteAsync();
        await (from g in dbContext.EvalRunGraderSummaries where g.EvalGraderId == evalGraderId select g).ExecuteDeleteAsync();

        dbContext.Remove(grader);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalColumns(List<EvalColumn> columns)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (columns.Count == 0)
            return;

        var ids = columns.Select(row => row.EvalColumnId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalColumns where ids.Contains(run.EvalColumnId) select run).ToListAsync();

        foreach (var column in columns)
        {
            var entity = entities.Where(e => e.EvalColumnId == column.EvalColumnId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalColumns.Add(column);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(column);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalColumn(Guid evalColumnId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var column = await (from c in dbContext.EvalColumns where c.EvalColumnId == evalColumnId select c).FirstOrDefaultAsync();

        if (column is null)
            throw new SharpOMaticException($"EvalColumn '{evalColumnId}' cannot be found.");

        await (from d in dbContext.EvalData where d.EvalColumnId == evalColumnId select d).ExecuteDeleteAsync();

        dbContext.Remove(column);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalRows(List<EvalRow> rows)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (rows.Count == 0)
            return;

        var ids = rows.Select(row => row.EvalRowId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalRows where ids.Contains(run.EvalRowId) select run).ToListAsync();

        foreach (var row in rows)
        {
            var entity = entities.Where(e => e.EvalRowId == row.EvalRowId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalRows.Add(row);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(row);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalRow(Guid evalRowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var row = await (from r in dbContext.EvalRows where r.EvalRowId == evalRowId select r).FirstOrDefaultAsync();

        if (row is null)
            throw new SharpOMaticException($"EvalRow '{evalRowId}' cannot be found.");

        await (from d in dbContext.EvalData where d.EvalRowId == evalRowId select d).ExecuteDeleteAsync();
        await (from r in dbContext.EvalRunRows where r.EvalRowId == evalRowId select r).ExecuteDeleteAsync();

        dbContext.Remove(row);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalData(List<EvalData> data)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (data.Count == 0)
            return;

        var ids = data.Select(row => row.EvalDataId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalData where ids.Contains(run.EvalDataId) select run).ToListAsync();

        foreach (var row in data)
        {
            var entity = entities.Where(e => e.EvalDataId == row.EvalDataId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalData.Add(row);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(row);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalData(Guid evalDataId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var data = await (from d in dbContext.EvalData where d.EvalDataId == evalDataId select d).FirstOrDefaultAsync();

        if (data is null)
            throw new SharpOMaticException($"EvalData '{evalDataId}' cannot be found.");

        dbContext.Remove(data);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalRun(EvalRun evalRun)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from run in dbContext.EvalRuns where run.EvalRunId == evalRun.EvalRunId select run).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.EvalRuns.Add(evalRun);
        else
        {
            dbContext.Entry(entity).CurrentValues.SetValues(evalRun);
            dbContext.Entry(entity).Property(run => run.CancelRequested).IsModified = false;
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalRunRows(List<EvalRunRow> runRows)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (runRows.Count == 0)
            return;

        var ids = runRows.Select(row => row.EvalRunRowId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalRunRows where ids.Contains(run.EvalRunRowId) select run).ToListAsync();

        foreach (var row in runRows)
        {
            var entity = entities.Where(e => e.EvalRunRowId == row.EvalRunRowId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalRunRows.Add(row);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(row);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalRunRowGraders(List<EvalRunRowGrader> runRowGraders)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (runRowGraders.Count == 0)
            return;

        var ids = runRowGraders.Select(row => row.EvalRunRowGraderId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalRunRowGraders where ids.Contains(run.EvalRunRowGraderId) select run).ToListAsync();

        foreach (var row in runRowGraders)
        {
            var entity = entities.Where(e => e.EvalRunRowGraderId == row.EvalRunRowGraderId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalRunRowGraders.Add(row);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(row);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalRunGraderSummaries(List<EvalRunGraderSummary> graderSummaries)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (graderSummaries.Count == 0)
            return;

        var ids = graderSummaries.Select(row => row.EvalRunGraderSummaryId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalRunGraderSummaries where ids.Contains(run.EvalRunGraderSummaryId) select run).ToListAsync();

        foreach (var summary in graderSummaries)
        {
            var entity = entities.Where(e => e.EvalRunGraderSummaryId == summary.EvalRunGraderSummaryId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalRunGraderSummaries.Add(summary);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(summary);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<List<EvalRunRowGrader>> GetEvalRunRowGraders(Guid evalRunId, Guid evalGraderId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await (from run in dbContext.EvalRuns.AsNoTracking() where run.EvalRunId == evalRunId select run).FirstOrDefaultAsync();
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var evalGrader = await (from grader in dbContext.EvalGraders.AsNoTracking() where grader.EvalGraderId == evalGraderId select grader).FirstOrDefaultAsync();
        if (evalGrader is null || evalGrader.EvalConfigId != evalRun.EvalConfigId)
            throw new SharpOMaticException($"EvalGrader '{evalGraderId}' does not belong to EvalRun '{evalRunId}'.");

        return await (
            from runRowGrader in dbContext.EvalRunRowGraders.AsNoTracking()
            where runRowGrader.EvalRunId == evalRunId && runRowGrader.EvalGraderId == evalGraderId
            select runRowGrader
        ).ToListAsync();
    }

    public async Task<int> GetEvalRunSummaryCount(Guid evalConfigId, string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRuns = dbContext.EvalRuns.AsNoTracking().Where(run => run.EvalConfigId == evalConfigId);
        evalRuns = ApplyEvalRunSearch(evalRuns, search);
        return await evalRuns.CountAsync();
    }

    public async Task<List<EvalRunSummary>> GetEvalRunSummaries(Guid evalConfigId, string? search, EvalRunSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRuns = dbContext.EvalRuns.AsNoTracking().Where(run => run.EvalConfigId == evalConfigId);
        evalRuns = ApplyEvalRunSearch(evalRuns, search);
        var sorted = GetSortedEvalRuns(evalRuns, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(run => new EvalRunSummary
            {
                EvalRunId = run.EvalRunId,
                EvalConfigId = run.EvalConfigId,
                Name = run.Name,
                Started = run.Started,
                Finished = run.Finished,
                Status = run.Status,
                Message = run.Message,
                Error = run.Error,
                TotalRows = run.TotalRows,
                CompletedRows = run.CompletedRows,
                FailedRows = run.FailedRows,
            })
            .ToListAsync();
    }

    public async Task<EvalRun> GetEvalRun(Guid evalRunId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.AsNoTracking().FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);

        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        return evalRun;
    }

    public async Task RequestCancelEvalRun(Guid evalRunId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        if (evalRun.Status != EvalRunStatus.Running)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' is not running and cannot be canceled.");

        if (evalRun.CancelRequested)
            return;

        evalRun.CancelRequested = true;
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalRun(Guid evalRunId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await (from run in dbContext.EvalRuns where run.EvalRunId == evalRunId select run).FirstOrDefaultAsync();
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        if (evalRun.Status == EvalRunStatus.Running)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' is currently running and cannot be deleted.");

        dbContext.Remove(evalRun);
        await dbContext.SaveChangesAsync();
    }

    public async Task<EvalRunDetail> GetEvalRunDetail(Guid evalRunId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.AsNoTracking().FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var evalRunSummary = new EvalRunSummary
        {
            EvalRunId = evalRun.EvalRunId,
            EvalConfigId = evalRun.EvalConfigId,
            Name = evalRun.Name,
            Started = evalRun.Started,
            Finished = evalRun.Finished,
            Status = evalRun.Status,
            Message = evalRun.Message,
            Error = evalRun.Error,
            TotalRows = evalRun.TotalRows,
            CompletedRows = evalRun.CompletedRows,
            FailedRows = evalRun.FailedRows,
        };

        var graderSummaries = await (
            from summary in dbContext.EvalRunGraderSummaries.AsNoTracking()
            join grader in dbContext.EvalGraders.AsNoTracking() on summary.EvalGraderId equals grader.EvalGraderId into graderJoin
            from grader in graderJoin.DefaultIfEmpty()
            where summary.EvalRunId == evalRunId
            orderby grader != null ? grader.Order : int.MaxValue, summary.EvalGraderId
            select new EvalRunGraderSummaryDetail
            {
                EvalRunGraderSummaryId = summary.EvalRunGraderSummaryId,
                EvalRunId = summary.EvalRunId,
                EvalGraderId = summary.EvalGraderId,
                Label = grader != null ? grader.Label : $"Grader {summary.EvalGraderId}",
                Order = grader != null ? grader.Order : int.MaxValue,
                PassThreshold = grader != null ? grader.PassThreshold : null,
                TotalCount = summary.TotalCount,
                CompletedCount = summary.CompletedCount,
                FailedCount = summary.FailedCount,
                MinScore = summary.MinScore,
                MaxScore = summary.MaxScore,
                AverageScore = summary.AverageScore,
                MedianScore = summary.MedianScore,
                StandardDeviation = summary.StandardDeviation,
                PassRate = summary.PassRate,
            }
        ).ToListAsync();

        return new EvalRunDetail { EvalRun = evalRunSummary, GraderSummaries = graderSummaries };
    }

    public async Task<int> GetEvalRunRowCount(Guid evalRunId, string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.AsNoTracking().FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var nameColumnId = await GetEvalRunNameColumnId(dbContext, evalRun.EvalConfigId);
        var rows = GetEvalRunRowProjections(dbContext, evalRunId, nameColumnId);
        rows = ApplyEvalRunRowSearch(rows, search);
        return await rows.CountAsync();
    }

    public async Task<List<EvalRunRowDetail>> GetEvalRunRows(Guid evalRunId, string? search, EvalRunRowSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.AsNoTracking().FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var nameColumnId = await GetEvalRunNameColumnId(dbContext, evalRun.EvalConfigId);
        var rows = GetEvalRunRowProjections(dbContext, evalRunId, nameColumnId);
        rows = ApplyEvalRunRowSearch(rows, search);
        rows = GetSortedEvalRunRows(rows, sortBy, sortDirection);

        if (skip > 0)
            rows = rows.Skip(skip);

        if (take > 0)
            rows = rows.Take(take);

        var rowPage = await rows.ToListAsync();

        var results = rowPage
            .Select(row => new EvalRunRowDetail
            {
                EvalRunRowId = row.EvalRunRowId,
                EvalRunId = row.EvalRunId,
                EvalRowId = row.EvalRowId,
                Name = row.Name,
                Order = row.Order,
                Status = row.Status,
                Started = row.Started,
                Finished = row.Finished,
                OutputContext = row.OutputContext,
                Error = row.Error,
                Graders = [],
            })
            .ToList();

        var evalRunRowIds = results.Select(row => row.EvalRunRowId).ToList();
        if (evalRunRowIds.Count == 0)
            return results;

        var graderDetails = await (
            from runRowGrader in dbContext.EvalRunRowGraders.AsNoTracking()
            join grader in dbContext.EvalGraders.AsNoTracking() on runRowGrader.EvalGraderId equals grader.EvalGraderId into graderJoin
            from grader in graderJoin.DefaultIfEmpty()
            where evalRunRowIds.Contains(runRowGrader.EvalRunRowId)
            orderby runRowGrader.EvalRunRowId, grader != null ? grader.Order : int.MaxValue, runRowGrader.EvalGraderId
            select new
            {
                runRowGrader.EvalRunRowId,
                Detail = new EvalRunRowGraderDetail
                {
                    EvalRunRowGraderId = runRowGrader.EvalRunRowGraderId,
                    EvalRunRowId = runRowGrader.EvalRunRowId,
                    EvalGraderId = runRowGrader.EvalGraderId,
                    Label = grader != null ? grader.Label : $"Grader {runRowGrader.EvalGraderId}",
                    Order = grader != null ? grader.Order : int.MaxValue,
                    Status = runRowGrader.Status,
                    Started = runRowGrader.Started,
                    Finished = runRowGrader.Finished,
                    Score = runRowGrader.Score,
                    OutputContext = runRowGrader.OutputContext,
                    Error = runRowGrader.Error,
                },
            }
        ).ToListAsync();

        var gradersByRowId = graderDetails.GroupBy(entry => entry.EvalRunRowId).ToDictionary(group => group.Key, group => group.Select(entry => entry.Detail).ToList());

        foreach (var row in results)
            if (gradersByRowId.TryGetValue(row.EvalRunRowId, out var graders))
                row.Graders = graders;

        return results;
    }

    private static IQueryable<EvalConfig> ApplyModelSearch(IQueryable<EvalConfig> evalConfigs, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return evalConfigs;

        var normalizedSearch = search.Trim().ToLower();
        return evalConfigs.Where(model => model.Name.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<EvalConfig> GetSortedModels(IQueryable<EvalConfig> models, EvalConfigSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            EvalConfigSortField.Description => sortDirection == SortDirection.Ascending
                ? models.OrderBy(model => model.Description).ThenBy(model => model.Name)
                : models.OrderByDescending(model => model.Description).ThenByDescending(model => model.Name),
            _ => sortDirection == SortDirection.Ascending
                ? models.OrderBy(model => model.Name).ThenBy(model => model.Description)
                : models.OrderByDescending(model => model.Name).ThenByDescending(model => model.Description),
        };
    }

    private static IQueryable<EvalRun> ApplyEvalRunSearch(IQueryable<EvalRun> evalRuns, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return evalRuns;

        var trimmedSearch = search.Trim();
        var normalizedSearch = trimmedSearch.ToLower();
        if (Guid.TryParse(trimmedSearch, out var parsedRunId))
        {
            return evalRuns.Where(run =>
                run.EvalRunId == parsedRunId
                || run.Name.ToLower().Contains(normalizedSearch)
                || (run.Message != null && run.Message.ToLower().Contains(normalizedSearch))
                || (run.Error != null && run.Error.ToLower().Contains(normalizedSearch))
            );
        }

        return evalRuns.Where(run =>
            run.Name.ToLower().Contains(normalizedSearch)
            || (run.Message != null && run.Message.ToLower().Contains(normalizedSearch))
            || (run.Error != null && run.Error.ToLower().Contains(normalizedSearch))
        );
    }

    private static IQueryable<EvalRun> GetSortedEvalRuns(IQueryable<EvalRun> evalRuns, EvalRunSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            EvalRunSortField.Name => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.Name).ThenByDescending(run => run.Started)
                : evalRuns.OrderByDescending(run => run.Name).ThenByDescending(run => run.Started),
            EvalRunSortField.Status => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.Status).ThenByDescending(run => run.Started)
                : evalRuns.OrderByDescending(run => run.Status).ThenByDescending(run => run.Started),
            EvalRunSortField.CompletedRows => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.CompletedRows).ThenByDescending(run => run.Started)
                : evalRuns.OrderByDescending(run => run.CompletedRows).ThenByDescending(run => run.Started),
            EvalRunSortField.FailedRows => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.FailedRows).ThenByDescending(run => run.Started)
                : evalRuns.OrderByDescending(run => run.FailedRows).ThenByDescending(run => run.Started),
            _ => sortDirection == SortDirection.Ascending ? evalRuns.OrderBy(run => run.Started) : evalRuns.OrderByDescending(run => run.Started),
        };
    }

    private static async Task<Guid?> GetEvalRunNameColumnId(SharpOMaticDbContext dbContext, Guid evalConfigId)
    {
        var nameColumnId = await dbContext
            .EvalColumns.AsNoTracking()
            .Where(column => column.EvalConfigId == evalConfigId && column.Name.ToLower() == "name")
            .OrderBy(column => column.Order)
            .Select(column => (Guid?)column.EvalColumnId)
            .FirstOrDefaultAsync();

        if (nameColumnId.HasValue)
            return nameColumnId;

        return await dbContext
            .EvalColumns.AsNoTracking()
            .Where(column => column.EvalConfigId == evalConfigId)
            .OrderBy(column => column.Order)
            .Select(column => (Guid?)column.EvalColumnId)
            .FirstOrDefaultAsync();
    }

    private static IQueryable<EvalRunRowProjection> GetEvalRunRowProjections(SharpOMaticDbContext dbContext, Guid evalRunId, Guid? nameColumnId)
    {
        return (
            from runRow in dbContext.EvalRunRows.AsNoTracking()
            where runRow.EvalRunId == evalRunId
            select new EvalRunRowProjection
            {
                EvalRunRowId = runRow.EvalRunRowId,
                EvalRunId = runRow.EvalRunId,
                EvalRowId = runRow.EvalRowId,
                Name = nameColumnId.HasValue
                    ? (from data in dbContext.EvalData.AsNoTracking() where data.EvalRowId == runRow.EvalRowId && data.EvalColumnId == nameColumnId.Value select data.StringValue).FirstOrDefault()
                        ?? ""
                    : "",
                Order = runRow.Order,
                Status = runRow.Status,
                Started = runRow.Started,
                Finished = runRow.Finished,
                OutputContext = runRow.OutputContext,
                Error = runRow.Error,
            }
        );
    }

    private static IQueryable<EvalRunRowProjection> ApplyEvalRunRowSearch(IQueryable<EvalRunRowProjection> rows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return rows;

        var normalizedSearch = search.Trim().ToLower();
        return rows.Where(row => row.Name.ToLower().Contains(normalizedSearch) || (row.Error != null && row.Error.ToLower().Contains(normalizedSearch)));
    }

    private static IQueryable<EvalRunRowProjection> GetSortedEvalRunRows(IQueryable<EvalRunRowProjection> rows, EvalRunRowSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            EvalRunRowSortField.Order => sortDirection == SortDirection.Ascending
                ? rows.OrderBy(row => row.Order).ThenBy(row => row.Name)
                : rows.OrderByDescending(row => row.Order).ThenBy(row => row.Name),
            EvalRunRowSortField.Status => sortDirection == SortDirection.Ascending
                ? rows.OrderBy(row => row.Status).ThenBy(row => row.Order)
                : rows.OrderByDescending(row => row.Status).ThenBy(row => row.Order),
            EvalRunRowSortField.Started => sortDirection == SortDirection.Ascending
                ? rows.OrderBy(row => row.Started).ThenBy(row => row.Order)
                : rows.OrderByDescending(row => row.Started).ThenBy(row => row.Order),
            _ => sortDirection == SortDirection.Ascending ? rows.OrderBy(row => row.Name).ThenBy(row => row.Order) : rows.OrderByDescending(row => row.Name).ThenBy(row => row.Order),
        };
    }

    private sealed class EvalRunRowProjection
    {
        public required Guid EvalRunRowId { get; set; }
        public required Guid EvalRunId { get; set; }
        public required Guid EvalRowId { get; set; }
        public required string Name { get; set; }
        public required int Order { get; set; }
        public required EvalRunStatus Status { get; set; }
        public required DateTime Started { get; set; }
        public DateTime? Finished { get; set; }
        public string? OutputContext { get; set; }
        public string? Error { get; set; }
    }

    // ------------------------------------------------
    // Setting Operations
    // ------------------------------------------------

    public async Task<List<Setting>> GetSettings()
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from s in dbContext.Settings.AsNoTracking() orderby s.Name select s).ToListAsync();
    }

    public async Task<Setting?> GetSetting(string name)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from s in dbContext.Settings.AsNoTracking() where s.Name == name select s).FirstOrDefaultAsync();
    }

    public async Task UpsertSetting(Setting model)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var setting = await (from s in dbContext.Settings where s.Name == model.Name select s).FirstOrDefaultAsync();

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

        var asset = await dbContext.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId);

        if (asset is null)
            throw new SharpOMaticException($"Asset '{assetId}' cannot be found.");

        return asset;
    }

    public async Task<int> GetAssetCount(AssetScope scope, string? search, Guid? runId = null)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var assets = dbContext.Assets.AsNoTracking().Where(a => a.Scope == scope);

        if (scope == AssetScope.Run && runId.HasValue)
            assets = assets.Where(a => a.RunId == runId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var searchLower = normalizedSearch.ToLower();
            assets = assets.Where(a => a.Name.ToLower().Contains(searchLower));
        }

        return await assets.CountAsync();
    }

    public async Task<List<Asset>> GetAssetsByScope(AssetScope scope, string? search, AssetSortField sortBy, SortDirection sortDirection, int skip, int take, Guid? runId = null)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var assets = dbContext.Assets.AsNoTracking().Where(a => a.Scope == scope);

        if (scope == AssetScope.Run && runId.HasValue)
            assets = assets.Where(a => a.RunId == runId.Value);

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
            AssetSortField.Created => sortDirection == SortDirection.Ascending ? assets.OrderBy(a => a.Created).ThenBy(a => a.Name) : assets.OrderByDescending(a => a.Created).ThenBy(a => a.Name),
            _ => sortDirection == SortDirection.Ascending ? assets.OrderBy(a => a.Name).ThenByDescending(a => a.Created) : assets.OrderByDescending(a => a.Name).ThenByDescending(a => a.Created),
        };
    }

    public async Task<List<Asset>> GetRunAssets(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.Assets.AsNoTracking().Where(a => a.RunId == runId).OrderByDescending(a => a.Created).ToListAsync();
    }

    public async Task<Asset?> GetRunAssetByName(Guid runId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();
        var normalizedName = name.Trim().ToLower();

        return await dbContext
            .Assets.AsNoTracking()
            .Where(a => a.Scope == AssetScope.Run && a.RunId == runId && a.Name.ToLower() == normalizedName)
            .OrderByDescending(a => a.Created)
            .FirstOrDefaultAsync();
    }

    public async Task<Asset?> GetLibraryAssetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();
        var normalizedName = name.Trim().ToLower();

        return await dbContext.Assets.AsNoTracking().Where(a => a.Scope == AssetScope.Library && a.Name.ToLower() == normalizedName).OrderByDescending(a => a.Created).FirstOrDefaultAsync();
    }

    public async Task UpsertAsset(Asset asset)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from a in dbContext.Assets where a.AssetId == asset.AssetId select a).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.Assets.Add(asset);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(asset);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsset(Guid assetId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var asset = await (from a in dbContext.Assets where a.AssetId == assetId select a).FirstOrDefaultAsync();

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

        var storageKeys = await dbContext.Assets.AsNoTracking().Where(a => a.RunId.HasValue && runIds.Contains(a.RunId.Value)).Select(a => a.StorageKey).ToListAsync();

        foreach (var storageKey in storageKeys)
            await assetStore.DeleteAsync(storageKey);
    }
}
