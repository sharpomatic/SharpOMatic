namespace SharpOMatic.Tests.Workflows;

public sealed class CodeNodeUnitTest
{
    [Fact]
    public async Task Code_does_nothing()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "")
            .Connect("start", "code")
            .Build();

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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "   ")
            .Connect("start", "code")
            .Build();

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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "Context.Set<bool>(\"should.not\", true);")
            .Connect("start", "code")
            .Build();

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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "Context.Set<bool>(\"input.boolean\", true);")
            .Connect("start", "code")
            .Build();

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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "Context.Set<int>(\"input.integer\", 42);")
            .Connect("start", "code")
            .Build();

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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "Context.Set<int>(\"output.value\", 7);")
            .Connect("start", "code")
            .Build();

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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "await Task.Delay(100);")
            .Connect("start", "code")
            .Build();

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
    public async Task Code_call_into_project()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "Context.Set<int>(\"output.integer\", WorkflowRunner.Double(21));")
            .Connect("start", "code")
            .Build();

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
            .AddCode("code", """"
                             var example = new ClassExample() 
                             { 
                                Success = true,
                                ErrorMessage = "Oops",
                                Scores = [1, 7]
                             };
                             Context.Set<ClassExample>("output.example", example);
                             """")
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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "this is not valid")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Code node failed compilation.", run.Error);
        Assert.Contains("error CS", run.Error);
    }

    [Fact]
    public async Task Code_compile_error_does_not_mutate_context()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "this is not valid")
            .Connect("start", "code")
            .Build();

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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "int a = ; int b = ; int c = ; int d = ;")
            .Connect("start", "code")
            .Build();

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
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "throw new System.InvalidOperationException(\"Boom\");")
            .Connect("start", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        var expected = $"Code node failed during execution.\n{Environment.NewLine}Boom";
        Assert.Equal(expected, run.Error);
    }

    [Fact]
    public async Task Code_runtime_error_does_not_mutate_context()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddCode("code", "throw new System.InvalidOperationException(\"Boom\");")
            .Connect("start", "code")
            .Build();

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
}
