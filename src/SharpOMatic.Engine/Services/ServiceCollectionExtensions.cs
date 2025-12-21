namespace SharpOMatic.Engine.Services;

public static class ServiceCollectionExtensions
{
    public static SharpOMaticBuilder AddSharpOMatic(this IServiceCollection services, Action<SharpOMaticOptions>? optionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<SharpOMaticOptions>();
        if (optionsAction is not null)
            services.Configure(optionsAction);

        // Add mandatory services
        services.TryAddSingleton<ICodeCheck, CodeCheckService>();
        services.TryAddSingleton<IRepository, RepositoryService>();
        services.TryAddSingleton<IEngine, EngineService>();
        services.TryAddSingleton<INodeQueue, NodeQueueService>();
        services.AddHostedService<NodeExecutionService>();

        // Add empty versions of optional services
        services.TryAddSingleton<IJsonConverterService>(_ => new JsonConverterService([]));
        services.TryAddSingleton<ISchemaTypeService>(_ => new SchemaTypeService([]));
        services.TryAddSingleton<IToolMethodRegistry>(_ => new ToolMethodRegistry([]));

        return new SharpOMaticBuilder(services);
    }
}

public static class SharpOMaticBuilderExtensions
{
    public static SharpOMaticBuilder AddJsonConverters(this SharpOMaticBuilder builder, IEnumerable<Type> converterTypes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(converterTypes);

        var converterTypeList = converterTypes.ToArray();

        builder.Services.RemoveAll<IJsonConverterService>();
        builder.Services.AddSingleton<IJsonConverterService>(_ => new JsonConverterService(converterTypeList));

        return builder;
    }

    public static SharpOMaticBuilder AddJsonConverters(this SharpOMaticBuilder builder, params Type[] converterTypes)
        => builder.AddJsonConverters((IEnumerable<Type>)converterTypes);

    public static SharpOMaticBuilder AddSchemaTypes(this SharpOMaticBuilder builder, IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(types);

        var typeList = types.ToArray();

        builder.Services.RemoveAll<ISchemaTypeService>();
        builder.Services.AddSingleton<ISchemaTypeService>(_ => new SchemaTypeService(typeList));

        return builder;
    }

    public static SharpOMaticBuilder AddSchemaTypes(this SharpOMaticBuilder builder, params Type[] types)
        => builder.AddSchemaTypes((IEnumerable<Type>)types);

    public static SharpOMaticBuilder AddToolMethods(this SharpOMaticBuilder builder, IEnumerable<Delegate> delegates)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(delegates);

        var methodList = delegates.ToArray();

        builder.Services.RemoveAll<IToolMethodRegistry>();
        builder.Services.AddSingleton<IToolMethodRegistry>(_ => new ToolMethodRegistry(methodList));

        return builder;
    }

    public static SharpOMaticBuilder AddToolMethods(this SharpOMaticBuilder builder, params Delegate[] methods)
        => builder.AddToolMethods((IEnumerable<Delegate>)methods);

    public static SharpOMaticBuilder AddRepository(
        this SharpOMaticBuilder builder,
        Action<DbContextOptionsBuilder> optionsAction,
        Action<SharpOMaticDbOptions>? dbOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(optionsAction);

        builder.Services.RemoveAll<IDbContextFactory<SharpOMaticDbContext>>();
        builder.Services.RemoveAll<DbContextOptions<SharpOMaticDbContext>>();

        builder.Services.AddDbContextFactory<SharpOMaticDbContext>(optionsAction);
        builder.Services.AddOptions<SharpOMaticDbOptions>();

        if (dbOptionsAction is not null)
            builder.Services.Configure(dbOptionsAction);

        return builder;
    }
}
