using SharpOMatic.Engine.Interfaces;

namespace SharpOMatic.Server.Services;

public class SharpOMaticNotification(IHubContext<NotificationHub> hubContext) : ISharpOMaticNotification
{
    public async Task RunProgress(Run model)
    {
        await hubContext.Clients.All.SendAsync("RunProgress", model);
    }

    public async Task TraceProgress(Trace model)
    {
        await hubContext.Clients.All.SendAsync("TraceProgress", model);
    }
}
