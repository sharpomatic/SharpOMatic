using SharpOMatic.Engine.Repository;
using System.Configuration;

namespace SharpOMatic.Engine.Services;

public class RepositoryService(IDbContextFactory<SharpOMaticDbContext> dbContextFactory) : IRepository
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new NodeEntityConverter() }
    };

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

    public async Task<ConnectionConfig?> GetConnectionConfig(string configId)
    {
        var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectionConfigMetadata
                              where c.ConfigId == configId
                              select c).AsNoTracking().FirstOrDefaultAsync();

        if (metadata is null)
            return null;

        return JsonSerializer.Deserialize<ConnectionConfig>(metadata.Config, _options);

    }

    public async Task<List<ConnectionConfig>> GetConnectionConfigs()
    {
        var dbContext = dbContextFactory.CreateDbContext();

        var entries = await dbContext.ConnectionConfigMetadata.AsNoTracking().ToListAsync();

        var results = new List<ConnectionConfig>();
        foreach (var entry in entries)
        {
            var config = JsonSerializer.Deserialize<ConnectionConfig>(entry.Config, _options);
            if (config != null)
                results.Add(config);
        }

        return results;
    }

    public async Task UpsertConnectionConfig(ConnectionConfig config)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectionConfigMetadata
                              where c.ConfigId == config.ConfigId
                              select c).FirstOrDefaultAsync();

        if (metadata is null)
        {
            metadata = new ConnectionConfigMetadata()
            {
                ConfigId = config.ConfigId,
                Config = ""
            };

            dbContext.ConnectionConfigMetadata.Add(metadata);
        }

        metadata.Config = JsonSerializer.Serialize(config, _options);
        await dbContext.SaveChangesAsync();
    }

    public IQueryable<ConnectionMetadata> GetConnections()
    {
        var dbContext = dbContextFactory.CreateDbContext();
        return dbContext.ConnectionMetadata;
    }

    public async Task<Connection> GetConnection(Guid connectionId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectionMetadata
                              where c.ConnectionId == connectionId
                              select c).AsNoTracking().FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Connection '{connectionId}' cannot be found.");

        var connection = JsonSerializer.Deserialize<Connection>(metadata.Config);

        if (connection is null)
            throw new SharpOMaticException($"Connection '{connectionId}' configuration is invalid.");

        return connection;
    }

    public async Task UpsertConnection(Connection connection)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entry = await (from c in dbContext.ConnectionMetadata
                           where c.ConnectionId == connection.ConnectionId
                           select c).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new ConnectionMetadata()
            {
                ConnectionId = connection.ConnectionId,
                Name = "",
                Description = "",
                Config = ""
            };

            dbContext.ConnectionMetadata.Add(entry);
        }

        entry.Name = connection.Name;
        entry.Description = connection.Description;
        entry.Config = JsonSerializer.Serialize(connection);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteConnection(Guid connectionId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectionMetadata
                              where c.ConnectionId == connectionId
                              select c).FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Connection '{connectionId}' cannot be found.");

        dbContext.Remove(metadata);
        await dbContext.SaveChangesAsync();
    }
}
