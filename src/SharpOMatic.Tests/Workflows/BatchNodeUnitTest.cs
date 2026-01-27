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
            .AddCode("left", "Context.Set(\"output\", true);")
            .AddCode("right", "Context.Set(\"output\", false);")
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
        Assert.True(outCtx.TryGetList("output", out var output));
        Assert.NotNull(output);
        Assert.Equal(2, output.Count);
        Assert.Contains(true, output);
        Assert.Contains(false, output);
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

    [Fact]
    public async Task Batch_fails_when_batch_size_invalid()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 0, parallelBatches: 1, inputPath: "list")
            .Connect("start", "batch")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Batch node batch size must be greater than or equal to 1.", run.Error);
    }

    [Fact]
    public async Task Batch_fails_when_parallel_batches_invalid()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 1, parallelBatches: 0, inputPath: "list")
            .Connect("start", "batch")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Batch node parallel batches must be greater than or equal to 1.", run.Error);
    }

    [Fact]
    public async Task Batch_fails_when_input_path_empty()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 1, parallelBatches: 1, inputPath: "")
            .Connect("start", "batch")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Batch node input array path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Batch_fails_when_input_path_missing()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 1, parallelBatches: 1, inputPath: "list")
            .Connect("start", "batch")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Batch node input array path 'list' could not be resolved.", run.Error);
    }

    [Fact]
    public async Task Batch_fails_when_input_path_not_list()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 1, parallelBatches: 1, inputPath: "value")
            .Connect("start", "batch")
            .Build();

        ContextObject ctx = [];
        ctx.Set("value", 123);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Batch node input array path 'value' must be a context list.", run.Error);
    }

    [Fact]
    public async Task Batch_empty_list_runs_continue_only()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 2, parallelBatches: 1, inputPath: "list")
            .AddCode("continue", "Context.Set<bool>(\"after.hit\", true);")
            .AddCode("process", "Context.Set<bool>(\"process.hit\", true);")
            .Connect("start", "batch")
            .Connect("batch.continue", "continue")
            .Connect("batch.process", "process")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", []);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.True(outCtx.Get<bool>("after.hit"));
        Assert.False(outCtx.TryGet<bool>("process.hit", out _));
    }

    [Fact]
    public async Task Batch_continue_runs_after_processing()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 2, parallelBatches: 2, inputPath: "list")
            .AddCode("process", "var list = Context.Get<ContextList>(\"list\"); Context.Set(\"output\", list);")
            .AddCode("continue", "var output = Context.Get<ContextList>(\"output\"); Context.Set<int>(\"after.count\", output.Count);")
            .Connect("start", "batch")
            .Connect("batch.process", "process")
            .Connect("batch.continue", "continue")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3, 4, 5]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        var output = outCtx.Get<ContextList>("output");
        Assert.Equal(5, output.Count);
        Assert.Equal(5, outCtx.Get<int>("after.count"));
    }

    [Fact]
    public async Task Batch_process_without_output_does_not_merge_output()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 2, parallelBatches: 2, inputPath: "list")
            .AddCode("process", "Context.Set<bool>(\"processed\", true);")
            .Connect("start", "batch")
            .Connect("batch.process", "process")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3, 4]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.False(outCtx.TryGetList("output", out _));
        Assert.False(outCtx.TryGet<bool>("processed", out _));
    }

    [Fact]
    public async Task Batch_process_failure_does_not_merge_output()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBatch(batchSize: 1, parallelBatches: 1, inputPath: "list")
            .AddCode("process", "throw new System.InvalidOperationException(\"Boom\");")
            .Connect("start", "batch")
            .Connect("batch.process", "process")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", ["boom", "other"]);

        var run = await WorkflowRunner.RunWorkflow(ctx, workflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Code node failed during execution.", run.Error);
        Assert.Contains("Boom", run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.False(outCtx.TryGetList("output", out _));
    }
}
