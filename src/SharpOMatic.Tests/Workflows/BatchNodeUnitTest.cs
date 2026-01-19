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

        var run = await WorkflowRunner.RunWorkflow([], workflow);

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
            .Connect("batch.batch", "code2")
            .Build();

        ContextObject ctx = [];
        ctx.Set<ContextList>("list", [1, 2, 3]);

        var run = await WorkflowRunner.RunWorkflow([], workflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);
        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.NotNull(outCtx);
        var list = outCtx.Get<ContextList>("list");
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.True(outCtx.Get<bool>("output[0]"));
        Assert.True(outCtx.Get<bool>("output[1]"));
        Assert.True(outCtx.Get<bool>("output[2]"));
    }
}
