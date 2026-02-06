namespace SharpOMatic.Engine.Services;

public class OverlayServiceProvider(IServiceProvider fallback, params object[] localServices) : IServiceProvider
{
    public object? GetService(Type serviceType)
    {
        foreach (var service in localServices)
            if (serviceType.IsInstanceOfType(service))
                return service;

        return fallback.GetService(serviceType);
    }
}
