namespace SharpOMatic.Tests.Workflows;

public sealed class FanOutInUnitTests
{
    [Fact]
    public async Task FanOutIn_merges_output_values()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "Context.Set<int>(\"output\", 1);")
            .AddCode("second", "Context.Set<int>(\"output\", 2);")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetList("output", out var list));
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Contains(1, list);
        Assert.Contains(2, list);
    }

    [Fact]
    public async Task FanOutIn_preserves_parent_context()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "Context.Set<int>(\"input.value\", 100); Context.Set<int>(\"output\", 1);")
            .AddCode("second", "Context.Set<int>(\"input.value\", 200); Context.Set<int>(\"output\", 2);")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Build();

        ContextObject ctx = [];
        ctx.Set<int>("input.value", 10);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(10, outCtx.Get<int>("input.value"));
        Assert.True(outCtx.TryGetList("output", out var list));
        Assert.NotNull(list);
        Assert.Equal(2, list.Count);
        Assert.Contains(1, list);
        Assert.Contains(2, list);
    }

    [Fact]
    public async Task FanOutIn_without_output_keeps_context()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "")
            .AddCode("second", "")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Build();

        ContextObject ctx = [];
        ctx.Set<int>("input.value", 10);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(10, outCtx.Get<int>("input.value"));
        Assert.False(outCtx.TryGetList("output", out var _));
    }

    [Fact]
    public async Task FanIn_requires_fanout_parent()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanIn("fanin")
            .Connect("start", "fanin")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Arriving thread did not originate from a Fan Out.", run.Error);
    }

    [Fact]
    public async Task FanOutIn_requires_single_fanin()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddFanIn("fanin1")
            .AddFanIn("fanin2")
            .Connect("start", "fanout")
            .Connect("fanout.first", "fanin1")
            .Connect("fanout.second", "fanin2")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("All incoming connections must originate from the same Fan Out.", run.Error);
    }

    [Fact]
    public async Task FanOut_requires_all_outputs_connected()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "Context.Set<int>(\"output\", 1);")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Cannot traverse output because it is not connected to another node.", run.Error);
    }

    [Fact]
    public async Task FanOutIn_merges_nested_objects()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "Context.Set<int>(\"output.a\", 1);")
            .AddCode("second", "Context.Set<int>(\"output.b\", 2);")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetObject("output", out var output));
        Assert.NotNull(output);
        Assert.Equal(1, output.Get<int>("a"));
        Assert.Equal(2, output.Get<int>("b"));
    }

    [Fact]
    public async Task FanOutIn_merges_list_and_scalar()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "Context.Set<ContextList>(\"output\", new ContextList { 1, 2 });")
            .AddCode("second", "await Task.Delay(50); Context.Set<int>(\"output\", 3);")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetList("output", out var list));
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.Contains(1, list);
        Assert.Contains(2, list);
        Assert.Contains(3, list);
    }

    [Fact]
    public async Task FanOutIn_single_branch_output_is_scalar()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "Context.Set<int>(\"output\", 42);")
            .AddCode("second", "")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(42, outCtx.Get<int>("output"));
        Assert.False(outCtx.TryGetList("output", out var _));
    }

    [Fact]
    public async Task FanOutIn_merges_three_branches()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second", "third"])
            .AddCode("first", "Context.Set<int>(\"output\", 1);")
            .AddCode("second", "Context.Set<int>(\"output\", 2);")
            .AddCode("third", "Context.Set<int>(\"output\", 3);")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("fanout.third", "third")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Connect("third", "fanin")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);

        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetList("output", out var list));
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.Contains(1, list);
        Assert.Contains(2, list);
        Assert.Contains(3, list);
    }

    [Fact]
    public async Task FanOutIn_merges_output_for_downstream_nodes()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "Context.Set<int>(\"output\", 1);")
            .AddCode("second", "Context.Set<int>(\"output\", 2);")
            .AddFanIn("fanin")
            .AddCode("code", "var list = Context.Get<ContextList>(\"output\"); Context.Set<int>(\"final.count\", list.Count);")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Connect("fanin", "code")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.Equal(2, outCtx.Get<int>("final.count"));
    }

    [Fact]
    public async Task FanOutIn_branch_failure_fails_run()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("fanout", ["first", "second"])
            .AddCode("first", "throw new System.InvalidOperationException(\"Boom\");")
            .AddCode("second", "Context.Set<int>(\"output\", 2);")
            .AddFanIn("fanin")
            .Connect("start", "fanout")
            .Connect("fanout.first", "first")
            .Connect("fanout.second", "second")
            .Connect("first", "fanin")
            .Connect("second", "fanin")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.StartsWith("Code node failed during execution.", run.Error);
    }

    [Fact]
    public async Task FanOutIn_nested_fanouts_merge_into_two_lists()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("root", ["left", "right"])
            .AddFanOut("leftOut", ["left1", "left2"])
            .AddFanOut("rightOut", ["right1", "right2"])
            .AddCode("left1", "Context.Set<int>(\"output.left\", 1);")
            .AddCode("left2", "Context.Set<int>(\"output.left\", 2);")
            .AddCode("right1", "Context.Set<int>(\"output.right\", 3);")
            .AddCode("right2", "Context.Set<int>(\"output.right\", 4);")
            .AddFanIn("leftIn")
            .AddFanIn("rightIn")
            .AddFanIn("finalIn")
            .Connect("start", "root")
            .Connect("root.left", "leftOut")
            .Connect("root.right", "rightOut")
            .Connect("leftOut.left1", "left1")
            .Connect("leftOut.left2", "left2")
            .Connect("rightOut.right1", "right1")
            .Connect("rightOut.right2", "right2")
            .Connect("left1", "leftIn")
            .Connect("left2", "leftIn")
            .Connect("right1", "rightIn")
            .Connect("right2", "rightIn")
            .Connect("leftIn", "finalIn")
            .Connect("rightIn", "finalIn")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetObject("output", out var output));
        Assert.NotNull(output);
        Assert.True(output.TryGetList("left", out var leftList));
        Assert.True(output.TryGetList("right", out var rightList));
        Assert.NotNull(leftList);
        Assert.NotNull(rightList);
        Assert.Equal(2, leftList.Count);
        Assert.Equal(2, rightList.Count);
        Assert.Contains(1, leftList);
        Assert.Contains(2, leftList);
        Assert.Contains(3, rightList);
        Assert.Contains(4, rightList);
    }

    [Fact]
    public async Task FanOutIn_nested_fanouts_merge_into_one_list()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddFanOut("root", ["left", "right"])
            .AddFanOut("leftOut", ["left1", "left2"])
            .AddFanOut("rightOut", ["right1", "right2"])
            .AddCode("left1", "Context.Set<int>(\"output\", 1);")
            .AddCode("left2", "Context.Set<int>(\"output\", 2);")
            .AddCode("right1", "Context.Set<int>(\"output\", 3);")
            .AddCode("right2", "Context.Set<int>(\"output\", 4);")
            .AddFanIn("leftIn")
            .AddFanIn("rightIn")
            .AddFanIn("finalIn")
            .Connect("start", "root")
            .Connect("root.left", "leftOut")
            .Connect("root.right", "rightOut")
            .Connect("leftOut.left1", "left1")
            .Connect("leftOut.left2", "left2")
            .Connect("rightOut.right1", "right1")
            .Connect("rightOut.right2", "right2")
            .Connect("left1", "leftIn")
            .Connect("left2", "leftIn")
            .Connect("right1", "rightIn")
            .Connect("right2", "rightIn")
            .Connect("leftIn", "finalIn")
            .Connect("rightIn", "finalIn")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetList("output", out var output));
        Assert.NotNull(output);
        Assert.Equal(4, output.Count);
        Assert.Contains(1, output);
        Assert.Contains(2, output);
        Assert.Contains(3, output);
        Assert.Contains(4, output);
    }
}
