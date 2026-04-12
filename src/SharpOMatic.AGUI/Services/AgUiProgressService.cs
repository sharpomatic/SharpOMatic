namespace SharpOMatic.AGUI.Services;

public sealed class AgUiProgressService(IAgUiRunEventBroker broker) : IProgressService
{
    public Task RunProgress(Run run)
    {
        broker.PublishRun(run);
        return Task.CompletedTask;
    }

    public Task TraceProgress(Run run, Trace trace)
    {
        return Task.CompletedTask;
    }

    public Task InformationsProgress(Run run, List<Information> informations)
    {
        return Task.CompletedTask;
    }

    public Task StreamEventProgress(Run run, List<StreamEvent> events)
    {
        broker.PublishStreamEvents(run.RunId, events);
        return Task.CompletedTask;
    }

    public Task EvalRunProgress(EvalRun evalRun)
    {
        return Task.CompletedTask;
    }
}
