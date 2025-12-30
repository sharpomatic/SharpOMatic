namespace SharpOMatic.Engine.Services;

public static class ServiceCollectionExtensions
{
    public static SharpOMaticBuilder AddSharpOMaticEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Add mandatory services
        services.TryAddSingleton<ICodeCheck, CodeCheckService>();
        services.TryAddSingleton<INodeQueueService, NodeQueueService>();
        services.TryAddSingleton<IRunNodeFactory, RunNodeFactory>();
        services.TryAddSingleton<IRunContextFactory, RunContextFactory>();
        services.TryAddScoped<IAssetService, AssetService>();
        services.TryAddScoped<IRepositoryService, RepositoryService>();
        services.TryAddScoped<IEngineService, EngineService>();
        services.AddHostedService<NodeExecutionService>();

        // Add empty versions of optional services
        services.TryAddSingleton<ISchemaTypeRegistry>(_ => new SchemaTypeRegistry([]));
        services.TryAddSingleton<IToolMethodRegistry>(_ => new ToolMethodRegistry([]));
        services.TryAddSingleton<IScriptOptionsService>(_ => new ScriptOptionsService([], []));
        services.TryAddSingleton<IJsonConverterService>(_ => new JsonConverterService([]));

        return new SharpOMaticBuilder(services);
    }
}
