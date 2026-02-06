using System;

namespace SharpOMatic.Engine.Services;

public class SharpOMaticBuilder
{
    public SharpOMaticBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    public IServiceCollection Services { get; }

    public SharpOMaticBuilder AddJsonConverters(IEnumerable<Type> converterTypes)
    {
        var converterTypeList = (converterTypes is null) ? [] : converterTypes.ToArray();

        Services.RemoveAll<IJsonConverterService>();
        Services.AddSingleton<IJsonConverterService>(_ => new JsonConverterService(converterTypeList));

        return this;
    }

    public SharpOMaticBuilder AddJsonConverters(params Type[] converterTypes) => AddJsonConverters((IEnumerable<Type>)converterTypes);

    public SharpOMaticBuilder AddSchemaTypes(IEnumerable<Type> types)
    {
        var typeList = (types is null) ? [] : types.ToArray();

        Services.RemoveAll<ISchemaTypeRegistry>();
        Services.AddSingleton<ISchemaTypeRegistry>(_ => new SchemaTypeRegistry(typeList));

        return this;
    }

    public SharpOMaticBuilder AddSchemaTypes(params Type[] types) => AddSchemaTypes((IEnumerable<Type>)types);

    public SharpOMaticBuilder AddToolMethods(IEnumerable<Delegate> delegates)
    {
        var methodList = (delegates is null) ? [] : delegates.ToArray();

        Services.RemoveAll<IToolMethodRegistry>();
        Services.AddSingleton<IToolMethodRegistry>(_ => new ToolMethodRegistry(methodList));

        return this;
    }

    public SharpOMaticBuilder AddToolMethods(params Delegate[] methods) => AddToolMethods((IEnumerable<Delegate>)methods);

    public SharpOMaticBuilder AddScriptOptions(IEnumerable<Assembly> assemblies, IEnumerable<string> imports)
    {
        var assemblyList = (assemblies is null) ? [] : assemblies.ToArray();
        var importList = (imports is null) ? [] : imports.ToArray();

        Services.RemoveAll<IScriptOptionsService>();
        Services.AddSingleton<IScriptOptionsService>(_ => new ScriptOptionsService(assemblyList, importList));

        return this;
    }

    public SharpOMaticBuilder AddScriptOptions(Assembly[] assemblies, string[] imports) => AddScriptOptions((IEnumerable<Assembly>)assemblies, (IEnumerable<string>)imports);

    public SharpOMaticBuilder AddRepository(Action<DbContextOptionsBuilder> optionsAction, Action<SharpOMaticDbOptions>? dbOptionsAction = null)
    {
        if (optionsAction is null)
            optionsAction = (_) => { };

        Services.RemoveAll<IDbContextFactory<SharpOMaticDbContext>>();
        Services.RemoveAll<DbContextOptions<SharpOMaticDbContext>>();

        Services.AddDbContextFactory<SharpOMaticDbContext>(optionsAction);
        Services.AddOptions<SharpOMaticDbOptions>();
        if (dbOptionsAction is not null)
            Services.Configure(dbOptionsAction);

        return this;
    }
}
