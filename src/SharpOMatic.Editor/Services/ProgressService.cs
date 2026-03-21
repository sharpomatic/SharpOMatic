namespace SharpOMatic.Editor.Services;

public class ProgressService(IHubContext<NotificationHub> hubContext) : IProgressService
{
    public async Task RunProgress(Run model)
    {
        if (!model.NeedsEditorEvents)
            return;

        await hubContext.Clients.All.SendAsync("RunProgress", model);
    }

    public async Task TraceProgress(Run run, Trace model)
    {
        if (!run.NeedsEditorEvents)
            return;

        await hubContext.Clients.All.SendAsync("TraceProgress", model);
    }

    public async Task InformationsProgress(Run run, List<Information> models)
    {
        if (!run.NeedsEditorEvents)
            return;

        await hubContext.Clients.All.SendAsync("InformationsProgress", models);
    }

    public async Task EvalRunProgress(EvalRun model)
    {
        await hubContext.Clients.All.SendAsync("EvalRunProgress", model);
    }
}
