using SharpOMatic.AGUI.Services;

namespace SharpOMatic.AGUI;

public static class SharpOMaticAgUiExtensions
{
    public static IServiceCollection AddSharpOMaticAgUi(this IServiceCollection services, string path)
    {
        ArgumentNullException.ThrowIfNull(services);
        var normalizedPath = NormalizePath(path);

        var mvcBuilder = services.AddControllers();
        EnsureApplicationPart(mvcBuilder, typeof(SharpOMaticAgUiExtensions).Assembly);
        mvcBuilder.AddMvcOptions(options => options.Conventions.Add(new AgUiControllerRouteConvention(normalizedPath)));

        services.TryAddSingleton<IAgUiRunEventBroker, AgUiRunEventBroker>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IProgressService, AgUiProgressService>());

        return services;
    }

    private static void EnsureApplicationPart(IMvcBuilder builder, Assembly assembly)
    {
        builder.ConfigureApplicationPartManager(manager =>
        {
            if (manager.ApplicationParts.OfType<AssemblyPart>().Any(part => part.Assembly == assembly))
                return;

            manager.ApplicationParts.Add(new AssemblyPart(assembly));
        });
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new SharpOMaticException("AG-UI path cannot be empty or whitespace.");

        return path.Trim().Trim('/');
    }
}

internal sealed class AgUiControllerRouteConvention(string path) : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType != typeof(Controllers.AgUiController).GetTypeInfo())
            return;

        var routeModel = new AttributeRouteModel(new RouteAttribute(path));
        if (controller.Selectors.Count == 0)
        {
            controller.Selectors.Add(new SelectorModel() { AttributeRouteModel = routeModel });
            return;
        }

        foreach (var selector in controller.Selectors)
            selector.AttributeRouteModel = routeModel;
    }
}
