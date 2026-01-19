namespace SharpOMatic.Tests.Workflows;

public sealed class EndNodeUnitTest
{
    [Fact]
    public async Task End_does_nothing()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEnd()
            .Connect("start", "end")
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
    public async Task End_mappings_copy_values()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEnd(
                "end",
                WorkflowBuilder.CreateOutputEntry("input.boolean", "output.boolean"),
                WorkflowBuilder.CreateOutputEntry("input.integer", "output.integer"))
            .Connect("start", "end")
            .Build();

        var endNode = (EndNodeEntity)workflow.Nodes.Single(node => node.NodeType == NodeType.End);
        endNode.ApplyMappings = true;

        ContextObject ctx = [];
        ctx.Set<bool>("input.boolean", true);
        ctx.Set<int>("input.integer", 42);
        ctx.Set<string>("input.ignored", "ignored");

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.Get<bool>("output.boolean"));
        Assert.Equal(42, outCtx.Get<int>("output.integer"));
        var hasInputBoolean = outCtx.TryGet<bool>("input.boolean", out var _);
        Assert.False(hasInputBoolean);
        var hasIgnored = outCtx.TryGet<string>("input.ignored", out var _);
        Assert.False(hasIgnored);
    }

    [Fact]
    public async Task End_mappings_skip_missing_values()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEnd(
                "end",
                WorkflowBuilder.CreateOutputEntry("input.present", "output.present"),
                WorkflowBuilder.CreateOutputEntry("input.missing", "output.missing"))
            .Connect("start", "end")
            .Build();

        var endNode = (EndNodeEntity)workflow.Nodes.Single(node => node.NodeType == NodeType.End);
        endNode.ApplyMappings = true;

        ContextObject ctx = [];
        ctx.Set<int>("input.present", 7);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(7, outCtx.Get<int>("output.present"));
        var hasMissing = outCtx.TryGet<int>("output.missing", out var _);
        Assert.False(hasMissing);
    }

    [Fact]
    public async Task End_mapping_empty_input_path()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEnd("end", WorkflowBuilder.CreateOutputEntry("", "output.value"))
            .Connect("start", "end")
            .Build();

        var endNode = (EndNodeEntity)workflow.Nodes.Single(node => node.NodeType == NodeType.End);
        endNode.ApplyMappings = true;

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("End node input path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task End_mapping_empty_output_path()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEnd("end", WorkflowBuilder.CreateOutputEntry("input.value", ""))
            .Connect("start", "end")
            .Build();

        var endNode = (EndNodeEntity)workflow.Nodes.Single(node => node.NodeType == NodeType.End);
        endNode.ApplyMappings = true;

        ContextObject ctx = [];
        ctx.Set<int>("input.value", 5);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("End node output path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task End_mappings_empty_clears_context()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEnd("end")
            .Connect("start", "end")
            .Build();

        var endNode = (EndNodeEntity)workflow.Nodes.Single(node => node.NodeType == NodeType.End);
        endNode.ApplyMappings = true;

        ContextObject ctx = [];
        ctx.Set<int>("input.value", 5);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Empty(outCtx);
        var hasValue = outCtx.TryGet<int>("input.value", out var _);
        Assert.False(hasValue);
    }

    [Fact]
    public async Task End_mappings_null_value_preserved()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEnd("end", WorkflowBuilder.CreateOutputEntry("input.nullValue", "output.nullValue"))
            .Connect("start", "end")
            .Build();

        var endNode = (EndNodeEntity)workflow.Nodes.Single(node => node.NodeType == NodeType.End);
        endNode.ApplyMappings = true;

        ContextObject ctx = [];
        ctx.Set<object?>("input.nullValue", null);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasNull = outCtx.TryGet<object?>("output.nullValue", out var value);
        Assert.True(hasNull);
        Assert.Null(value);
    }

    [Fact]
    public async Task End_mappings_invalid_output_path_skips_value()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEnd("end", WorkflowBuilder.CreateOutputEntry("input.value", "output[0]"))
            .Connect("start", "end")
            .Build();

        var endNode = (EndNodeEntity)workflow.Nodes.Single(node => node.NodeType == NodeType.End);
        endNode.ApplyMappings = true;

        ContextObject ctx = [];
        ctx.Set<int>("input.value", 5);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var hasList = outCtx.TryGetList("output", out var _);
        Assert.False(hasList);
        var hasValue = outCtx.TryGet<int>("output[0]", out var _);
        Assert.False(hasValue);
        Assert.Empty(outCtx);
    }

    [Fact]
    public async Task End_wins_over_late_branch_without_fanin()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["fast", "slow"])
            .AddCode("fast", "Context.Set<string>(\"output.winner\", \"end\"); Context.Set<string>(\"output.fastOnly\", \"fast\");")
            .AddCode("slow", "await Task.Delay(200); Context.Set<string>(\"output.winner\", \"slow\"); Context.Set<string>(\"output.slowOnly\", \"slow\");")
            .AddEnd("end")
            .Connect("start", "fanout")
            .Connect("fanout.fast", "fast")
            .Connect("fanout.slow", "slow")
            .Connect("fast", "end")
            .Build();

        var endNode = (EndNodeEntity)workflow.Nodes.Single(node => node.NodeType == NodeType.End);
        endNode.ApplyMappings = false;

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("end", outCtx.Get<string>("output.winner"));
        Assert.Equal("fast", outCtx.Get<string>("output.fastOnly"));
        var hasSlowOnly = outCtx.TryGet<string>("output.slowOnly", out var _);
        Assert.False(hasSlowOnly);
    }

    [Fact]
    public async Task End_multiple_end_nodes_last_one_wins()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["fast", "slow"])
            .AddCode("fast", "Context.Set<string>(\"output.winner\", \"fast\");")
            .AddCode("slow", "await Task.Delay(200); Context.Set<string>(\"output.winner\", \"slow\");")
            .AddEnd("endFast")
            .AddEnd("endSlow")
            .Connect("start", "fanout")
            .Connect("fanout.fast", "fast")
            .Connect("fanout.slow", "slow")
            .Connect("fast", "endFast")
            .Connect("slow", "endSlow")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal("slow", outCtx.Get<string>("output.winner"));
    }
}
