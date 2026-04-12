using SharpOMatic.AGUI.Services;

namespace SharpOMatic.AGUI;

public static class SharpOMaticAgUiExtensions
{
    private const string DefaultBasePath = "/sharpomatic";
    private const string DefaultChildPath = "/api/agui";

    public static IServiceCollection AddSharpOMaticAgUi(this IServiceCollection services, string? basePath = null, string? childPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var normalizedPath = NormalizeFullPath(basePath, childPath);

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

    private static string NormalizeFullPath(string? basePath, string? childPath)
    {
        if (string.IsNullOrWhiteSpace(childPath) && LooksLikeLegacyFullPath(basePath))
        {
            var legacyPath = NormalizePath(basePath!, "basePath");
            var apiIndex = legacyPath.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
            if (apiIndex > 0)
            {
                basePath = legacyPath[..apiIndex];
                childPath = legacyPath[apiIndex..];
            }
        }

        var normalizedBasePath = NormalizeBasePath(basePath);
        var normalizedChildPath = NormalizePath(string.IsNullOrWhiteSpace(childPath) ? DefaultChildPath : childPath, "childPath");
        return $"{normalizedBasePath.Trim('/')}{normalizedChildPath}";
    }

    private static string NormalizeBasePath(string? basePath)
    {
        var normalizedBasePath = NormalizePath(string.IsNullOrWhiteSpace(basePath) ? DefaultBasePath : basePath, "basePath");
        if (string.Equals(normalizedBasePath, "/api", StringComparison.OrdinalIgnoreCase))
            throw new SharpOMaticException("AG-UI base path must be a non-empty sub-path like '/sharpomatic'.");

        return normalizedBasePath;
    }

    private static string NormalizePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new SharpOMaticException($"AG-UI {parameterName} cannot be empty or whitespace.");

        var trimmed = path.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
            trimmed = "/" + trimmed;

        trimmed = trimmed.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed) || string.Equals(trimmed, "/", StringComparison.Ordinal))
            throw new SharpOMaticException($"AG-UI {parameterName} must be a non-empty sub-path.");

        return trimmed;
    }

    private static bool LooksLikeLegacyFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim();
        return trimmed.Contains("/api/", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("/agui", StringComparison.OrdinalIgnoreCase);
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
