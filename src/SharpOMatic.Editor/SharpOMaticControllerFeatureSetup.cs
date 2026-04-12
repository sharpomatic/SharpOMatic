namespace SharpOMatic.Editor;

internal class SharpOMaticControllerToggle
{
    public bool EnableEditor { get; set; }
    public bool EnableTransfer { get; set; }
    public bool RouteConventionConfigured { get; set; }
}

internal class SharpOMaticControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private readonly SharpOMaticControllerToggle toggle;

    public SharpOMaticControllerFeatureProvider(SharpOMaticControllerToggle toggle)
    {
        this.toggle = toggle ?? throw new ArgumentNullException(nameof(toggle));
    }

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        if (toggle.EnableEditor && toggle.EnableTransfer)
            return;

        var editorAssembly = typeof(SharpOMaticEditorExtensions).Assembly;
        for (var i = feature.Controllers.Count - 1; i >= 0; i--)
        {
            var controller = feature.Controllers[i];
            if (controller.Assembly != editorAssembly)
                continue;

            var isTransfer = controller.AsType() == typeof(TransferController);
            if (isTransfer)
            {
                if (!toggle.EnableTransfer)
                    feature.Controllers.RemoveAt(i);
            }
            else if (!toggle.EnableEditor)
            {
                feature.Controllers.RemoveAt(i);
            }
        }
    }
}

internal static class SharpOMaticControllerFeatureSetup
{
    private const string RoutePrefix = "sharpomatic";

    public static SharpOMaticControllerToggle GetOrAddToggle(IServiceCollection services)
    {
        var existing = services.FirstOrDefault(service => service.ServiceType == typeof(SharpOMaticControllerToggle));
        if (existing?.ImplementationInstance is SharpOMaticControllerToggle toggle)
            return toggle;

        toggle = new SharpOMaticControllerToggle();
        services.AddSingleton(toggle);
        return toggle;
    }

    public static void EnsureFeatureProvider(IMvcBuilder builder, SharpOMaticControllerToggle toggle)
    {
        builder.ConfigureApplicationPartManager(manager =>
        {
            if (manager.FeatureProviders.OfType<SharpOMaticControllerFeatureProvider>().Any())
                return;

            manager.FeatureProviders.Add(new SharpOMaticControllerFeatureProvider(toggle));
        });
    }

    public static void EnsureRouteConvention(IMvcBuilder builder, SharpOMaticControllerToggle toggle)
    {
        if (toggle.RouteConventionConfigured)
            return;

        builder.AddMvcOptions(options =>
        {
            options.Conventions.Add(new SharpOMaticControllerRouteConvention(RoutePrefix));
        });

        toggle.RouteConventionConfigured = true;
    }

    public static void EnsureApplicationPart(IMvcBuilder builder, Assembly assembly)
    {
        builder.ConfigureApplicationPartManager(manager =>
        {
            if (manager.ApplicationParts.OfType<AssemblyPart>().Any(part => part.Assembly == assembly))
                return;

            manager.ApplicationParts.Add(new AssemblyPart(assembly));
        });
    }
}

internal sealed class SharpOMaticControllerRouteConvention(string routePrefix) : IControllerModelConvention
{
    public string RoutePrefix { get; } = routePrefix;

    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType.Assembly != typeof(SharpOMaticEditorExtensions).Assembly)
            return;

        var prefixRoute = new AttributeRouteModel(new RouteAttribute(RoutePrefix));
        foreach (var selector in controller.Selectors)
        {
            if (selector.AttributeRouteModel is null)
            {
                selector.AttributeRouteModel = prefixRoute;
                continue;
            }

            selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(prefixRoute, selector.AttributeRouteModel);
        }
    }
}
