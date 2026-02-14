namespace SharpOMatic.DemoServer;

public class ProgressService(IServiceProvider serviceProvider) : IProgressService
{
    public async Task RunProgress(Run model)
    {
        if (model.RunStatus == RunStatus.Failed)
        {
            // Log or otherwise process a failure as needed
            Console.WriteLine($"Failed with error {model.Error}");
        }
        else if (model.RunStatus == RunStatus.Success)
        {
            // Deserialize needs access to the customized list of json converters
            var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
            ContextObject context = ContextObject.Deserialize(model.OutputContext, jsonConverters);

            // Perform success actions here
        }
    }

    public async Task TraceProgress(Trace model) { }

    public async Task EvalRunProgress(EvalRun model) { }
}
