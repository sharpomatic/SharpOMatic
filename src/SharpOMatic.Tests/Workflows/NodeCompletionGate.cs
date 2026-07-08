namespace SharpOMatic.Tests.Workflows;

// Test helper that lets one workflow branch deterministically wait until a node in another branch
// has finished executing, instead of relying on wall-clock delays that flake under a loaded thread
// pool. Register it in the test provider and resolve it from a code node script via ServiceProvider.
public sealed class NodeCompletionGate : IProgressService
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new();

    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<NodeCompletionGate>();
        services.AddSingleton<IProgressService>(sp => sp.GetRequiredService<NodeCompletionGate>());
    }

    public Task WaitForNode(string nodeTitle)
    {
        return GetGate(nodeTitle).Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    public Task TraceProgress(Run run, Trace trace)
    {
        if (trace.NodeStatus == NodeStatus.Success)
            GetGate(trace.Title).TrySetResult();

        return Task.CompletedTask;
    }

    public Task RunProgress(Run run) => Task.CompletedTask;

    public Task InformationsProgress(Run run, List<Information> informations) => Task.CompletedTask;

    public Task StreamEventProgress(Run run, List<StreamEventProgressItem> events) => Task.CompletedTask;

    public Task EvalRunProgress(EvalRun evalRun) => Task.CompletedTask;

    private TaskCompletionSource GetGate(string nodeTitle)
    {
        return _gates.GetOrAdd(nodeTitle, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }
}
