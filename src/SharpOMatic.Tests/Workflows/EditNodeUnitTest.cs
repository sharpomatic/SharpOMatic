namespace SharpOMatic.Tests.Workflows;

public sealed class EditNodeUnitTest
{
    [Fact]
    public async Task Edit_does_nothing()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit().Connect("start", "edit").Build();

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
    public async Task Edit_delete_removes_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateDeleteEntry("input.boolean")).Connect("start", "edit").Build();

        ContextObject ctx = [];
        ctx.Set<bool>("input.boolean", true);
        ctx.Set<int>("input.integer", 42);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasBoolean = outCtx.TryGet<bool>("input.boolean", out var _);
        Assert.False(hasBoolean);
        Assert.Equal(42, outCtx.Get<int>("input.integer"));
    }

    [Fact]
    public async Task Edit_upsert_sets_value()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateIntUpsert("output.integer", 123)).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(123, outCtx.Get<int>("output.integer"));
    }

    [Fact]
    public async Task Edit_delete_and_upsert()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEdit("edit", WorkflowBuilder.CreateDeleteEntry("input.remove"), WorkflowBuilder.CreateStringUpsert("input.add", "added"))
            .Connect("start", "edit")
            .Build();

        ContextObject ctx = [];
        ctx.Set<string>("input.remove", "delete-me");
        ctx.Set<string>("input.keep", "keep-me");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasRemoved = outCtx.TryGet<string>("input.remove", out var _);
        Assert.False(hasRemoved);
        Assert.Equal("keep-me", outCtx.Get<string>("input.keep"));
        Assert.Equal("added", outCtx.Get<string>("input.add"));
    }

    [Fact]
    public async Task Edit_move_moves_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateMoveEntry("input.source", "input.target")).Connect("start", "edit").Build();

        ContextObject ctx = [];
        ctx.Set<string>("input.source", "moved");
        ctx.Set<string>("input.keep", "keep-me");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasSource = outCtx.TryGet<string>("input.source", out var _);
        Assert.False(hasSource);
        Assert.Equal("moved", outCtx.Get<string>("input.target"));
        Assert.Equal("keep-me", outCtx.Get<string>("input.keep"));
    }

    [Fact]
    public async Task Edit_duplicate_copies_string()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateDuplicateEntry("input.source", "input.target")).Connect("start", "edit").Build();

        ContextObject ctx = [];
        ctx.Set<string>("input.source", "copied");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("copied", outCtx.Get<string>("input.source"));
        Assert.Equal("copied", outCtx.Get<string>("input.target"));
    }

    [Fact]
    public async Task Edit_duplicate_copies_object()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateDuplicateEntry("input.source", "input.target")).Connect("start", "edit").Build();

        ContextObject ctx = [];
        ContextObject child = [];
        child.Set("str", "copied");
        child.Set("boolean", true);
        ctx.Set("input.source", child);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var sourceCtx = outCtx.Get<ContextObject>("input.source");
        var targetCtx = outCtx.Get<ContextObject>("input.target");
        Assert.NotNull(sourceCtx);
        Assert.NotNull(targetCtx);
        Assert.Equal("copied", sourceCtx.Get<string>("str"));
        Assert.Equal("copied", targetCtx.Get<string>("str"));
        Assert.True(sourceCtx.Get<bool>("boolean"));
        Assert.True(targetCtx.Get<bool>("boolean"));
    }

    [Fact]
    public async Task Edit_move_missing_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateMoveEntry("input.missing", "input.target")).Connect("start", "edit").Build();

        ContextObject ctx = [];
        ctx.Set<string>("input.keep", "keep-me");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasTarget = outCtx.TryGet<string>("input.target", out var _);
        Assert.False(hasTarget);
        Assert.Equal("keep-me", outCtx.Get<string>("input.keep"));
    }

    [Fact]
    public async Task Edit_duplicate_missing_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateDuplicateEntry("input.missing", "input.target")).Connect("start", "edit").Build();

        ContextObject ctx = [];
        ctx.Set<string>("input.keep", "keep-me");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasTarget = outCtx.TryGet<string>("input.target", out var _);
        Assert.False(hasTarget);
        Assert.Equal("keep-me", outCtx.Get<string>("input.keep"));
    }

    [Fact]
    public async Task Edit_move_duplicate_upsert_delete_order()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEdit(
                "edit",
                WorkflowBuilder.CreateMoveEntry("input.value", "output.moved"),
                WorkflowBuilder.CreateDuplicateEntry("output.moved", "output.copy"),
                WorkflowBuilder.CreateStringUpsert("output.moved", "upserted"),
                WorkflowBuilder.CreateDeleteEntry("output.copy")
            )
            .Connect("start", "edit")
            .Build();

        ContextObject ctx = [];
        ctx.Set<string>("input.value", "initial");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasInputValue = outCtx.TryGet<string>("input.value", out var _);
        Assert.False(hasInputValue);
        var hasOutputCopy = outCtx.TryGet<string>("output.copy", out var _);
        Assert.False(hasOutputCopy);
        Assert.Equal("upserted", outCtx.Get<string>("output.moved"));
    }

    [Fact]
    public async Task Edit_delete_missing_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateDeleteEntry("input.missing")).Connect("start", "edit").Build();

        ContextObject ctx = [];
        ctx.Set<string>("input.keep", "keep-me");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasMissing = outCtx.TryGet<string>("input.missing", out var _);
        Assert.False(hasMissing);
        Assert.Equal("keep-me", outCtx.Get<string>("input.keep"));
    }

    [Fact]
    public async Task Edit_delete_empty_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateDeleteEntry("")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Edit node delete path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Edit_move_empty_from_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateMoveEntry("", "output.to")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Edit node move 'from' path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Edit_move_empty_to_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateMoveEntry("input.from", "")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Edit node move 'to' path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Edit_duplicate_empty_from_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateDuplicateEntry("", "output.to")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Edit node duplicate 'from' path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Edit_duplicate_empty_to_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateDuplicateEntry("input.from", "")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Edit node duplicate 'to' path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_empty_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateStringUpsert("", "value")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Edit node upsert path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_invalid_path()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateIntUpsert("input.list[0]", 1)).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Edit node entry 'input.list[0]' could not be assigned the value.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_invalid_bool_value()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateUpsertEntry("input.boolean", ContextEntryType.Bool, "not-bool")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.boolean' value could not be parsed as boolean.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_invalid_int_value()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateUpsertEntry("input.integer", ContextEntryType.Int, "not-int")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.integer' value could not be parsed as an int.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_invalid_double_value()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateUpsertEntry("input.double", ContextEntryType.Double, "not-double")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.double' value could not be parsed as a double.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_invalid_json_value()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateUpsertEntry("input.json", ContextEntryType.JSON, "{ invalid")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.json' value could not be parsed as json.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_expression_compile_error()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateUpsertEntry("input.expr", ContextEntryType.Expression, "missing")).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Input entry 'input.expr' expression failed compilation.", run.Error);
        Assert.Contains("error CS0103", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_expression_runtime_error()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEdit("edit", WorkflowBuilder.CreateUpsertEntry("input.expr", ContextEntryType.Expression, "throw new System.InvalidOperationException(\"Boom\");"))
            .Connect("start", "edit")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        var expected = $"Input entry 'input.expr' expression failed during execution.\n{Environment.NewLine}Boom";
        Assert.Equal(expected, run.Error);
    }

    [Fact]
    public async Task Edit_upsert_invalid_asset_ref_value()
    {
        var workflow = new WorkflowBuilder().AddStart().AddEdit("edit", WorkflowBuilder.CreateUpsertEntry("input.asset", ContextEntryType.AssetRef, string.Empty)).Connect("start", "edit").Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.asset' asset reference cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_invalid_asset_ref_list_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEdit("edit", WorkflowBuilder.CreateUpsertEntry("input.assets", ContextEntryType.AssetRefList, string.Empty))
            .Connect("start", "edit")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Input entry 'input.assets' asset list cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Edit_upsert_then_delete_same_path()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEdit("edit", WorkflowBuilder.CreateStringUpsert("input.value", "new"), WorkflowBuilder.CreateDeleteEntry("input.value"))
            .Connect("start", "edit")
            .Build();

        ContextObject ctx = [];
        ctx.Set<string>("input.value", "old");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasValue = outCtx.TryGet<string>("input.value", out var _);
        Assert.False(hasValue);
    }
}
