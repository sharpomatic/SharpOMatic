namespace SharpOMatic.Editor.Services;

public class ProgressService(IHubContext<NotificationHub> hubContext) : IProgressService
{
    public async Task RunProgress(Run model)
    {
        await hubContext.Clients.All.SendAsync("RunProgress", model);
    }

    public async Task TraceProgress(Trace model)
    {
        await hubContext.Clients.All.SendAsync("TraceProgress", model);
    }

    public async Task InformationsProgress(List<Information> models)
    {
        await hubContext.Clients.All.SendAsync("InformationsProgress", models);
    }

    public async Task EvalRunProgress(EvalRun model)
    {
        await hubContext.Clients.All.SendAsync("EvalRunProgress", model);
    }
}
