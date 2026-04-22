namespace SharpOMatic.Tests.Workflows;

public sealed class ActivityStreamEventSerializationUnitTests
{
    [Fact]
    public async Task Activity_delta_from_context_emits_plain_json_patch_without_type_metadata()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """
                var details = new ContextObject();
                details.Add("status", "running");

                var activity = new ContextObject();
                activity.Add("title", new string('x', 200));
                activity.Add("details", details);
                Context.Set("activity", activity);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");

                var updatedDetails = new ContextObject();
                updatedDetails.Add("status", "completed");
                updatedDetails.Add("summary", "done");
                Context.Set("activity.details", updatedDetails);

                await Events.AddActivitySyncFromContextAsync("activity-1", "PLAN", "activity");
                """
            )
            .AddEnd()
            .Connect("start", "code")
            .Connect("code", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            var deltaEvent = Assert.Single(streamEvents, e => e.EventKind == StreamEventKind.ActivityDelta);

            Assert.DoesNotContain("\"$type\"", deltaEvent.TextDelta, StringComparison.Ordinal);

            using var payload = JsonDocument.Parse(deltaEvent.TextDelta!);
            Assert.Equal(JsonValueKind.Array, payload.RootElement.ValueKind);
            Assert.Equal(2, payload.RootElement.GetArrayLength());

            Dictionary<string, JsonElement> operationsByPath = [];
            foreach (var operation in payload.RootElement.EnumerateArray())
                operationsByPath[operation.GetProperty("path").GetString()!] = operation;

            Assert.Equal("replace", operationsByPath["/details/status"].GetProperty("op").GetString());
            Assert.Equal("completed", operationsByPath["/details/status"].GetProperty("value").GetString());
            Assert.Equal("add", operationsByPath["/details/summary"].GetProperty("op").GetString());
            Assert.Equal("done", operationsByPath["/details/summary"].GetProperty("value").GetString());
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }
}
