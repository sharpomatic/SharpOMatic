namespace SharpOMatic.Engine.Services;

public class RunContextFactory(IServiceScopeFactory scopeFactory) : IRunContextFactory
{
    public RunContext Create(
        WorkflowEntity workflow,
        Run run,
        IEnumerable<JsonConverter> jsonConverters,
        int runNodeLimit,
        TaskCompletionSource<Run>? completionSource)
    {
        var scope = scopeFactory.CreateScope();

        var Servicerepository = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
        var progressService = scope.ServiceProvider.GetRequiredService<IProgressService>();
        var toolMethodRegistry = scope.ServiceProvider.GetRequiredService<IToolMethodRegistry>();
        var schemaTypeRegistry = scope.ServiceProvider.GetRequiredService<ISchemaTypeRegistry>();
        var scriptOptionsService = scope.ServiceProvider.GetRequiredService<IScriptOptionsService>();

        return new RunContext(
            scope,
            Servicerepository,
            progressService,
            toolMethodRegistry,
            schemaTypeRegistry,
            scriptOptionsService,
            jsonConverters,
            workflow,
            run,
            runNodeLimit,
            completionSource);
    }
}
