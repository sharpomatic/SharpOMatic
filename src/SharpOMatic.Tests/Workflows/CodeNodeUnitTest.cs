namespace SharpOMatic.Tests.Workflows;

public sealed class CodeNodeUnitTest
{
    [Fact]
    public async Task Code_does_nothing()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "").Connect("start", "code").Build();

        ContextObject ctx = [];
        ctx.Set<bool>("input.boolean", true);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.Get<bool>("input.boolean"));
    }

    [Fact]
    public async Task Code_whitespace_is_noop()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "   ").Connect("start", "code").Build();

        ContextObject ctx = [];
        ctx.Set<bool>("input.boolean", true);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.Get<bool>("input.boolean"));
    }

    [Fact]
    public async Task Code_null_is_noop()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "Context.Set<bool>(\"should.not\", true);").Connect("start", "code").Build();

        var codeNode = workflow.Nodes.OfType<CodeNodeEntity>().Single();
        codeNode.Code = null!;

        ContextObject ctx = [];
        ctx.Set<bool>("input.boolean", true);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.Get<bool>("input.boolean"));
        Assert.False(outCtx.TryGet<bool>("should.not", out _));
    }

    [Fact]
    public async Task Code_adds_context()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "Context.Set<bool>(\"input.boolean\", true);").Connect("start", "code").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.Get<bool>("input.boolean"));
    }

    [Fact]
    public async Task Code_modifies_context()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "Context.Set<int>(\"input.integer\", 42);").Connect("start", "code").Build();

        ContextObject ctx = [];
        ctx.Set<int>("input.integer", 21);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(42, outCtx.Get<int>("input.integer"));
    }

    [Fact]
    public async Task Code_no_output_connection_completes()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "Context.Set<int>(\"output.value\", 7);").Connect("start", "code").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal(7, outCtx.Get<int>("output.value"));
    }

    [Fact]
    public async Task Code_allows_async()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "await Task.Delay(100);").Connect("start", "code").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
    }

    [Fact]
    public async Task Code_can_access_service_provider()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "var svc = ServiceProvider.GetService(typeof(SharpOMatic.Engine.Interfaces.IJsonConverterService)); Context.Set<bool>(\"service.ok\", svc is not null);")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.True(outCtx.Get<bool>("service.ok"));
    }

    [Fact]
    public async Task Code_templates_expand_context_and_text_assets()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "var prompt = await Templates.ExpandAsync(\"Summarize {{$input.topic}} using <<prompt-base.txt>>\"); Context.Set(\"output.prompt\", prompt);")
            .Connect("start", "code")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        var assetService = new AssetService(repositoryService, provider.GetRequiredService<IAssetStore>());
        await assetService.CreateFromBytesAsync(Encoding.UTF8.GetBytes("base for {{$input.topic}}"), "prompt-base.txt", "text/plain", AssetScope.Library);

        ContextObject ctx = [];
        ctx.Set("input.topic", "templates");

        var run = await RunWorkflowWithProvider(provider, ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("Summarize templates using base for templates", outCtx.Get<string>("output.prompt"));
    }

    [Fact]
    public async Task Code_call_into_project()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "Context.Set<int>(\"output.integer\", WorkflowRunner.Double(21));").Connect("start", "code").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(42, outCtx.Get<int>("output.integer"));
    }

    [Fact]
    public async Task Code_return_custom_class()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode(
                "code",
                """"
                var example = new ClassExample() 
                { 
                   Success = true,
                   ErrorMessage = "Oops",
                   Scores = [1, 7]
                };
                Context.Set<ClassExample>("output.example", example);
                """"
            )
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var services = new ServiceCollection();
        await using var provider = WorkflowRunner.BuildProvider();
        var converterService = provider.GetRequiredService<IJsonConverterService>();
        var outCtx = ContextObject.Deserialize(run.OutputContext, converterService);
        Assert.NotNull(outCtx);
        var example = outCtx.Get<ClassExample>("output.example");
        Assert.True(example.Success);
        Assert.Equal("Oops", example.ErrorMessage);
        var list = example.Scores;
        Assert.NotNull(list);
        Assert.Equal(2, list.Length);
        Assert.Equal(1, list[0]);
        Assert.Equal(7, list[1]);
    }

    [Fact]
    public async Task Code_compile_error()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "this is not valid").Connect("start", "code").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Code node failed compilation.", run.Error);
        Assert.Contains("error CS", run.Error);
    }

    [Fact]
    public async Task Code_compile_error_does_not_mutate_context()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "this is not valid").Connect("start", "code").Build();

        ContextObject ctx = [];
        ctx.Set<int>("input.value", 5);
        ctx.Set<string>("input.name", "Ada");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal(5, outCtx.Get<int>("input.value"));
        Assert.Equal("Ada", outCtx.Get<string>("input.name"));
    }

    [Fact]
    public async Task Code_compile_error_truncates_diagnostics()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "int a = ; int b = ; int c = ; int d = ;").Connect("start", "code").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Code node failed compilation.", run.Error);
        var errorText = run.Error ?? string.Empty;
        var matches = System.Text.RegularExpressions.Regex.Matches(errorText, "error CS");
        Assert.True(matches.Count <= 3, $"Expected <= 3 diagnostics, got {matches.Count}.");
    }

    [Fact]
    public async Task Code_runtime_error()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "throw new System.InvalidOperationException(\"Boom\");").Connect("start", "code").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        var expected = $"Code node failed during execution.\n{Environment.NewLine}Boom";
        Assert.Equal(expected, run.Error);
    }

    [Fact]
    public async Task Code_template_cycle_fails_during_execution()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "var prompt = await Templates.ExpandAsync(\"{{$template}}\"); Context.Set(\"output.prompt\", prompt);")
            .Connect("start", "code")
            .Build();

        ContextObject ctx = [];
        ctx.Set("template", "{{$template}}");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Code node failed during execution.", run.Error);
        Assert.Contains("Recursive context template expansion detected", run.Error);
    }

    [Fact]
    public async Task Code_runtime_error_does_not_mutate_context()
    {
        var workflow = new WorkflowBuilder().AddStart().AddCode("code", "throw new System.InvalidOperationException(\"Boom\");").Connect("start", "code").Build();

        ContextObject ctx = [];
        ctx.Set<int>("input.value", 5);
        ctx.Set<string>("input.name", "Ada");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal(5, outCtx.Get<int>("input.value"));
        Assert.Equal("Ada", outCtx.Get<string>("input.name"));
    }

    private static async Task<Run> RunWorkflowWithProvider(ServiceProvider provider, ContextObject ctx, WorkflowEntity workflow)
    {
        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engine.StartWorkflowRunAndWait(workflow.Id, ctx);
            await Task.Delay(100);
            return run;
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }
}
