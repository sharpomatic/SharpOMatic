namespace SharpOMatic.Tests.Workflows;

public sealed class SwitchNodeUnitTest
{
    [Fact]
    public async Task Switch_default_only_takes_default()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("default", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_always_takes_first()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "true"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("first", "Context.Set<string>(\"result\", \"first\");")
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.first", "first")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("first", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_always_takes_default()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "false"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("first", "Context.Set<string>(\"result\", \"first\");")
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.first", "first")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("default", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_matches_first()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch(
                "switch",
                new WorkflowBuilder.SwitchChoice("first", "return Context.Get<int>(\"value\") < 50;"),
                new WorkflowBuilder.SwitchChoice("second", "return Context.Get<int>(\"value\") < 100;"),
                new WorkflowBuilder.SwitchChoice("default", "")
            )
            .AddCode("first", "Context.Set<string>(\"result\", \"first\");")
            .AddCode("second", "Context.Set<string>(\"result\", \"second\");")
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.first", "first")
            .Connect("switch.second", "second")
            .Connect("switch.default", "default")
            .Build();

        ContextObject ctx = [];
        ctx.Set<int>("value", 42);
        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("first", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_matches_second()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch(
                "switch",
                new WorkflowBuilder.SwitchChoice("first", "return Context.Get<int>(\"value\") < 50;"),
                new WorkflowBuilder.SwitchChoice("second", "return Context.Get<int>(\"value\") < 100;"),
                new WorkflowBuilder.SwitchChoice("default", "")
            )
            .AddCode("first", "Context.Set<string>(\"result\", \"first\");")
            .AddCode("second", "Context.Set<string>(\"result\", \"second\");")
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.first", "first")
            .Connect("switch.second", "second")
            .Connect("switch.default", "default")
            .Build();

        ContextObject ctx = [];
        ctx.Set<int>("value", 99);
        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("second", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_matches_default()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch(
                "switch",
                new WorkflowBuilder.SwitchChoice("first", "return Context.Get<int>(\"value\") < 50;"),
                new WorkflowBuilder.SwitchChoice("second", "return Context.Get<int>(\"value\") < 100;"),
                new WorkflowBuilder.SwitchChoice("default", "")
            )
            .AddCode("first", "Context.Set<string>(\"result\", \"first\");")
            .AddCode("second", "Context.Set<string>(\"result\", \"second\");")
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.first", "first")
            .Connect("switch.second", "second")
            .Connect("switch.default", "default")
            .Build();

        ContextObject ctx = [];
        ctx.Set<int>("value", 101);
        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("default", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_default_output_must_be_connected()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "true"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("first", "Context.Set<string>(\"result\", \"first\");")
            .Connect("start", "switch")
            .Connect("switch.first", "first")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Switch node must have an output connection on the last output connector.", run.Error);
    }

    [Fact]
    public async Task Switch_entry_returning_null_fails()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "return null;"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Switch node entry 'first' returned null instead of a boolean value.", run.Error);
    }

    [Fact]
    public async Task Switch_entry_returning_non_bool_fails()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "return 123;"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Switch node entry 'first' return type 'System.Int32' instead of a boolean value.", run.Error);
    }

    [Fact]
    public async Task Switch_entry_compile_error_fails()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "return missing;"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Switch node entry 'first' failed compilation.", run.Error);
        Assert.Contains("error CS0103", run.Error);
    }

    [Fact]
    public async Task Switch_entry_execution_error_fails()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "throw new System.InvalidOperationException(\"Boom\");"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Switch node entry 'first' failed during execution.", run.Error);
        Assert.Contains("Boom", run.Error);
    }

    [Fact]
    public async Task Switch_skips_matched_unconnected_output()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "true"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("default", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_skips_unconnected_match_and_takes_next_connected()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "true"), new WorkflowBuilder.SwitchChoice("second", "true"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("second", "Context.Set<string>(\"result\", \"second\");")
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.second", "second")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("second", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_ignores_whitespace_entry_code()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "   "), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("default", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Switch_compile_error_truncates_diagnostics()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddSwitch("switch", new WorkflowBuilder.SwitchChoice("first", "return missing1 + missing2 + missing3 + missing4;"), new WorkflowBuilder.SwitchChoice("default", ""))
            .AddCode("default", "Context.Set<string>(\"result\", \"default\");")
            .Connect("start", "switch")
            .Connect("switch.default", "default")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Switch node entry 'first' failed compilation.", run.Error);
        var errorText = run.Error ?? string.Empty;
        var matches = System.Text.RegularExpressions.Regex.Matches(errorText, "error CS");
        Assert.True(matches.Count <= 3, $"Expected <= 3 diagnostics, got {matches.Count}.");
    }
}
