using Microsoft.CodeAnalysis;

namespace SharpOMatic.Engine.Services;

public class RepositoryService(IDbContextFactory<SharpOMaticDbContext> dbContextFactory) : IRepository
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
    public IQueryable<Workflow> GetWorkflows()
    {
        var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.Workflows;
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

        dbContext.Remove(workflow);
        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Run Operations
    // ------------------------------------------------
    public IQueryable<Run> GetRuns()
    {
        var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.Runs;
    }

    public IQueryable<Run> GetWorkflowRuns(Guid workflowId)
    {
        var dbContext = dbContextFactory.CreateDbContext();
        return (from r in dbContext.Runs
                where r.WorkflowId == workflowId
                select r);
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


    // ------------------------------------------------
    // Trace Operations
    // ------------------------------------------------
    public IQueryable<Trace> GetTraces()
    {
        var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.Traces;
    }

    public IQueryable<Trace> GetRunTraces(Guid runId)
    {
        var dbContext = dbContextFactory.CreateDbContext();

        return (from t in dbContext.Traces
                where t.RunId == runId
                select t);
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
        var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorConfigMetadata
                              where c.ConfigId == configId
                              select c).AsNoTracking().FirstOrDefaultAsync();

        if (metadata is null)
            return null;

        return JsonSerializer.Deserialize<ConnectorConfig>(metadata.Config, _options);

    }

    public async Task<List<ConnectorConfig>> GetConnectorConfigs()
    {
        var dbContext = dbContextFactory.CreateDbContext();

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
    public IQueryable<ConnectorMetadata> GetConnectors()
    {
        var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.ConnectorMetadata;
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
                            if ((field.Type == FieldDescriptorType.Secret) && 
                                connector.FieldValues.ContainsKey(field.Name) &&
                                entryConfig.FieldValues.ContainsKey(field.Name) &&
                                (connector.FieldValues[field.Name] != SECRET_OBFUSCATION))
                            {
                                connector.FieldValues[field.Name] = entryConfig.FieldValues[field.Name];
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

    // ------------------------------------------------
    // ModelConfig Operations
    // ------------------------------------------------
    public async Task<ModelConfig?> GetModelConfig(string configId)
    {
        var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelConfigMetadata
                              where m.ConfigId == configId
                              select m).AsNoTracking().FirstOrDefaultAsync();

        if (metadata is null)
            return null;

        return JsonSerializer.Deserialize<ModelConfig>(metadata.Config, _options);

    }

    public async Task<List<ModelConfig>> GetModelConfigs()
    {
        var dbContext = dbContextFactory.CreateDbContext();

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
    public IQueryable<ModelMetadata> GetModels()
    {
        var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.ModelMetadata;
    }

    public async Task<Model> GetModel(Guid modelId)
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
        if ((model.ParameterValues.Count > 0) && !string.IsNullOrWhiteSpace(model.ConfigId))
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
}
