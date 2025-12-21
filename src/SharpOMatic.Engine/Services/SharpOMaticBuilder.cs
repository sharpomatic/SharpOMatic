namespace SharpOMatic.Engine.Services;

public sealed class SharpOMaticBuilder
{
    public SharpOMaticBuilder(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    public IServiceCollection Services { get; }
}
