namespace SharpOMatic.Tests.Workflows;

public sealed class BatchNodeUnitTest
{
    [Fact]
    public async Task Batch_does_nothing()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(inputPath: "list")
            .Connect("start", "batch")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var list = outCtx.Get<ContextList>("list");
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public async Task Batch_continue_no_batch()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(inputPath: "list")
            .AddCode("code", "Context.Set<bool>(\"code\", true);")
            .Connect("start", "batch")
            .Connect("batch.continue", "code")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var list = outCtx.Get<ContextList>("list");
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.True(outCtx.Get<bool>("code"));
    }

    [Fact]
    public async Task Batch_continue_and_single_batch()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(inputPath: "list")
            .AddCode("code1", "Context.Set<bool>(\"code\", true);")
            .AddCode("code2", "Context.Set<bool>(\"output\", true);")
            .Connect("start", "batch")
            .Connect("batch.continue", "code1")
            .Connect("batch.process", "code2")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var list = outCtx.Get<ContextList>("list");
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.True(outCtx.Get<bool>("output"));
    }

    [Fact]
    public async Task Batch_process_outputs_preserve_order()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 2, parallelBatches: 2, inputPath: "list")
            .AddCode("code", "var list = Context.Get<ContextList>(\"list\"); Context.Set(\"output\", list);")
            .Connect("start", "batch")
            .Connect("batch.process", "code")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3, 4, 5]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetList("output", out var outputList));
        Assert.NotNull(outputList);
        Assert.Equal(5, outputList.Count);
        Assert.Equal(1, (int)outputList[0]!);
        Assert.Equal(2, (int)outputList[1]!);
        Assert.Equal(3, (int)outputList[2]!);
        Assert.Equal(4, (int)outputList[3]!);
        Assert.Equal(5, (int)outputList[4]!);
    }

    [Fact]
    public async Task Batch_process_99_items_8_parallel()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 2, parallelBatches: 8, inputPath: "list")
            .AddCode("code", "var list = Context.Get<ContextList>(\"list\"); Context.Set(\"output\", list);")
            .Connect("start", "batch")
            .Connect("batch.process", "code")
            .Build();

        int numbers = 99;
        ContextObject ctx = [];
        ContextList list = [];
        foreach (int i in Enumerable.Range(0, numbers))
            list.Add(i);
        ctx.Set("list", list);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var outputList = outCtx.Get<ContextList>("output");
        Assert.NotNull(outputList);
        foreach (int i in Enumerable.Range(0, numbers))
            Assert.Equal(i, (int)outputList[i]!);
    }

    [Fact]
    public async Task Batch_process_with_fanout_fanin_runs_both_paths_()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 10, parallelBatches: 1, inputPath: "list")
            .AddFanOut("fanout", ["left", "right"])
            .AddCode("left", "Context.Set(\"output.left\", true);")
            .AddCode("right", "Context.Set(\"output.right\", true);")
            .AddFanIn("fanin")
            .Connect("start", "batch")
            .Connect("batch.process", "fanout")
            .Connect("fanout.left", "left")
            .Connect("fanout.right", "right")
            .Connect("left", "fanin")
            .Connect("right", "fanin")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetObject("output", out var output));
        Assert.NotNull(output);
        Assert.True(output.Get<bool>("left"));
        Assert.True(output.Get<bool>("right"));
    }

    [Fact]
    public async Task Batch_process_with_fanout_no_fanin_runs_both_paths()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 10, parallelBatches: 1, inputPath: "list")
            .AddFanOut("fanout", ["left", "right"])
            .AddCode("left", "Context.Set(\"output.left\", true);")
            .AddCode("right", "Context.Set(\"output.right\", true);")
            .Connect("start", "batch")
            .Connect("batch.process", "fanout")
            .Connect("fanout.left", "left")
            .Connect("fanout.right", "right")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        Assert.True(outCtx.TryGetObject("output", out var output));
        Assert.NotNull(output);

        // NOTE: The returned context is from the last path to finish because
        // it did not have FanIn. No FanIn so no merging of multiple paths
    }
}
