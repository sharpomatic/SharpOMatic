using SharpOMatic.Engine.Contexts;

namespace SharpOMatic.Tests.Workflows;

public sealed class StartNodeUnitTest
{
    [Fact]
    public async Task Workflow_must_have_start()
    {
        var workflow = new WorkflowBuilder()
            .Build();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() => WorkflowRunner.RunWorkflow([], workflow));
        Assert.Equal("Must have exactly one start node.", exception.Message);
    }

    [Fact]
    public async Task Workflow_cannot_have_2_start()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddStart()
            .Build();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() => WorkflowRunner.RunWorkflow([], workflow));
        Assert.Equal("Must have exactly one start node.", exception.Message);
    }

    [Fact]
    public async Task Start_no_init()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .Build();

        ContextObject ctx = [];
        ctx.Set<bool>("input.boolean", true);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Success, run.RunStatus);

        // Final context is the one passed into the start node
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.Get<bool>("input.boolean"));
    }

    [Fact]
    public async Task Start_init_mandatory_present()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateBoolInput("input.boolean", false))
            .Build();

        ContextObject ctx = [];
        ctx.Set<bool>("input.boolean", true);
        ctx.Set<int>("input.integer", 42);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Success, run.RunStatus);

        // Final context is new with only input.boolean copied into it
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.Get<bool>("input.boolean"));
        var hasInteger = outCtx.TryGet<int>("input.integer", out var _);
        Assert.False(hasInteger);
    }

    [Fact]
    public async Task Start_init_mandatory_missing_path()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateBoolInput("", false))
            .Build();

        ContextObject ctx = [];
        ctx.Set<int>("input.integer", 42);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Start node path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Start_init_mandatory_missing_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateBoolInput("input.boolean", false))
            .Build();

        ContextObject ctx = [];
        ctx.Set<int>("input.integer", 42);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Start node mandatory path 'input.boolean' cannot be resolved.", run.Error);
    }

    [Fact]
    public async Task Start_init_optional()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateBoolInput("input.boolean", true, true))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Success, run.RunStatus);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.Get<bool>("input.boolean"));
    }

    [Fact]
    public async Task Start_init_json_parsed()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateJsonInput("input.json", true, "[42, 3.14, false]"))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Success, run.RunStatus);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(42, outCtx.Get<int>("input.json[0]"));
        Assert.Equal(3.14, outCtx.Get<double>("input.json[1]"));
        Assert.False(outCtx.Get<bool>("input.json[2]"));
    }

    [Fact]
    public async Task Start_init_expression()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateExpressionInput("input.expr", true, "return 2 + 2;"))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Success, run.RunStatus);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(4, outCtx.Get<int>("input.expr"));
    }

    [Fact]
    public async Task Start_init_optional_invalid_bool_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateInputEntry("input.boolean", true, ContextEntryType.Bool, "not-bool"))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.boolean' value could not be parsed as boolean.", run.Error);
    }

    [Fact]
    public async Task Start_init_optional_invalid_int_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateInputEntry("input.integer", true, ContextEntryType.Int, "not-int"))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.integer' value could not be parsed as an int.", run.Error);
    }

    [Fact]
    public async Task Start_init_optional_invalid_double_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateInputEntry("input.double", true, ContextEntryType.Double, "not-double"))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.double' value could not be parsed as a double.", run.Error);
    }

    [Fact]
    public async Task Start_init_optional_invalid_json_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateInputEntry("input.json", true, ContextEntryType.JSON, "{ invalid"))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.json' value could not be parsed as json.", run.Error);
    }

    [Fact]
    public async Task Start_init_optional_expression_compile_error()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateInputEntry("input.expr", true, ContextEntryType.Expression, "missing"))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Input entry 'input.expr' expression failed compilation.", run.Error);
        Assert.Contains("error CS0103", run.Error);
    }

    [Fact]
    public async Task Start_init_optional_expression_runtime_error()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateInputEntry("input.expr", true, ContextEntryType.Expression, "throw new System.InvalidOperationException(\"Boom\");"))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        var expected = $"Input entry 'input.expr' expression failed during execution.\n{Environment.NewLine}Boom";
        Assert.Equal(expected, run.Error);
    }

    [Fact]
    public async Task Start_init_optional_invalid_asset_ref_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateInputEntry("input.asset", true, ContextEntryType.AssetRef, string.Empty))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.asset' asset reference cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Start_init_optional_invalid_asset_ref_list_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateInputEntry("input.assets", true, ContextEntryType.AssetRefList, string.Empty))
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.assets' asset list cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Start_init_provided_list_index_not_copied()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateIntInput("input.list[0]", false))
            .Build();

        ContextObject ctx = [];
        ctx.Set("input.list", new ContextList { 123 });

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Start node cannot set 'input.list[0]' into context.", run.Error);
    }

    [Fact]
    public async Task Start_init_null_input_treated_as_provided()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true, WorkflowBuilder.CreateStringInput("input.nullValue", false))
            .Build();

        ContextObject ctx = [];
        ctx.Set<object?>("input.nullValue", null);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Success, run.RunStatus);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasValue = outCtx.TryGet<object?>("input.nullValue", out var value);
        Assert.True(hasValue);
        Assert.Null(value);
    }

    [Fact]
    public async Task Start_init_no_entries_clears_context()
    {
        var workflow = new WorkflowBuilder()
            .AddStart("start", true)
            .Build();

        ContextObject ctx = [];
        ctx.Set<int>("input.integer", 42);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Success, run.RunStatus);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Empty(outCtx);
        var hasInteger = outCtx.TryGet<int>("input.integer", out var _);
        Assert.False(hasInteger);
    }
}
