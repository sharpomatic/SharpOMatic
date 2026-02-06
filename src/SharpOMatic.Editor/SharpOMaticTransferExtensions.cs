namespace SharpOMatic.Editor;

public static class SharpOMaticTransferExtensions
{
    public static IServiceCollection AddSharpOMaticTransfer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var mvcBuilder = services.AddControllers();
        SharpOMaticControllerFeatureSetup.EnsureApplicationPart(
            mvcBuilder,
            typeof(TransferController).Assembly
        );

        var toggle = SharpOMaticControllerFeatureSetup.GetOrAddToggle(services);
        toggle.EnableTransfer = true;
        SharpOMaticControllerFeatureSetup.EnsureFeatureProvider(mvcBuilder, toggle);

        services.TryAddScoped<ITransferService, TransferService>();

        return services;
    }
}
