namespace SharpOMatic.Engine.Interfaces;

public interface IRepository
{
    IQueryable<Workflow> GetWorkflows();
    Task<WorkflowEntity> GetWorkflow(Guid workflowId);
    Task UpsertWorkflow(WorkflowEntity workflow);
    Task DeleteWorkflow(Guid workflowId);

    IQueryable<Run> GetRuns();
    IQueryable<Run> GetWorkflowRuns(Guid workflowId);
    Task UpsertRun(Run run);

    IQueryable<Trace> GetTraces();
    IQueryable<Trace> GetRunTraces(Guid runId);
    Task UpsertTrace(Trace trace);

    Task<List<ConnectionConfig>> GetConnectionConfigs();
    Task<ConnectionConfig?> GetConnectionConfig(string configId);
    Task UpsertConnectionConfig(ConnectionConfig config);

    IQueryable<ConnectionMetadata> GetConnections();
    Task<Connection> GetConnection(Guid connectionId);
    Task UpsertConnection(Connection connection);
    Task DeleteConnection(Guid connectionId);
}
