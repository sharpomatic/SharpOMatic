namespace SharpOMatic.Tests.Workflows;

public sealed class GosubNodeUnitTest
{
    [Fact]
    public async Task Gosub_merges_child_context_on_return()
    {
        var subWorkflowId = Guid.NewGuid();
        var subWorkflow = new WorkflowBuilder()
            .WithId(subWorkflowId)
            .AddStart()
            .AddCode("sub", "Context.Set<int>(\"child.counter\", Context.Get<int>(\"counter\") + 1); Context.Set<string>(\"child.flag\", \"yes\");")
            .AddEnd()
            .Connect("start", "sub")
            .Connect("sub", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddCode("seed", "Context.Set<int>(\"counter\", 1); Context.Set<string>(\"parent\", \"ok\");")
            .AddGosub("gosub", subWorkflowId)
            .AddCode("after", "Context.Set<int>(\"result\", Context.Get<int>(\"child.counter\"));")
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "gosub")
            .Connect("gosub", "after")
            .Connect("after", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, subWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal(1, outCtx.Get<int>("counter"));
        Assert.Equal("ok", outCtx.Get<string>("parent"));
        Assert.Equal(2, outCtx.Get<int>("child.counter"));
        Assert.Equal("yes", outCtx.Get<string>("child.flag"));
        Assert.Equal(2, outCtx.Get<int>("result"));
    }

    [Fact]
    public async Task Gosub_end_does_not_override_without_end()
    {
        var subWorkflowId = Guid.NewGuid();
        var subWorkflow = new WorkflowBuilder()
            .WithId(subWorkflowId)
            .AddStart()
            .AddCode("sub", "Context.Set<string>(\"value\", \"sub\");")
            .AddEnd()
            .Connect("start", "sub")
            .Connect("sub", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", subWorkflowId)
            .AddCode("after", "Context.Set<string>(\"value\", \"parent\"); Context.Set<string>(\"result\", \"after\");")
            .Connect("start", "gosub")
            .Connect("gosub", "after")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, subWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("parent", outCtx.Get<string>("value"));
        Assert.Equal("after", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Gosub_allows_missing_output_connection()
    {
        var subWorkflowId = Guid.NewGuid();
        var subWorkflow = new WorkflowBuilder()
            .WithId(subWorkflowId)
            .AddStart()
            .AddCode("sub", "Context.Set<string>(\"child\", \"ok\");")
            .AddEnd()
            .Connect("start", "sub")
            .Connect("sub", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddCode("seed", "Context.Set<string>(\"parent\", \"yes\");")
            .AddGosub("gosub", subWorkflowId)
            .Connect("start", "seed")
            .Connect("seed", "gosub")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, subWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("yes", outCtx.Get<string>("parent"));
        Assert.Equal("ok", outCtx.Get<string>("child"));
    }

    [Fact]
    public async Task Gosub_fanout_paths_and_merges_on_fanin()
    {
        var subWorkflowId = Guid.NewGuid();
        var subWorkflow = new WorkflowBuilder()
            .WithId(subWorkflowId)
            .AddStart()
            .AddCode("sub", "Context.Set<string>(\"output\", Context.Get<string>(\"path\"));")
            .AddEnd()
            .Connect("start", "sub")
            .Connect("sub", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddFanOut("fanout", ["left", "right"])
            .AddCode("left-path", "Context.Set<string>(\"path\", \"left\");")
            .AddGosub("left-gosub", subWorkflowId)
            .AddCode("right-path", "Context.Set<string>(\"path\", \"right\");")
            .AddGosub("right-gosub", subWorkflowId)
            .AddFanIn("fanin")
            .AddEnd()
            .Connect("start", "fanout")
            .Connect("fanout.left", "left-path")
            .Connect("left-path", "left-gosub")
            .Connect("left-gosub", "fanin")
            .Connect("fanout.right", "right-path")
            .Connect("right-path", "right-gosub")
            .Connect("right-gosub", "fanin")
            .Connect("fanin", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, subWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        var outputs = outCtx.Get<ContextList>("output");
        Assert.Equal(2, outputs.Count);
        var values = outputs.Select(value => value?.ToString()).ToList();
        Assert.Contains("left", values);
        Assert.Contains("right", values);
    }

    [Fact]
    public async Task Gosub_fanout_without_fanin_returns_last()
    {
        var subWorkflowId = Guid.NewGuid();
        var subWorkflow = new WorkflowBuilder()
            .WithId(subWorkflowId)
            .AddStart()
            .AddFanOut("fanout", ["left", "right"])
            .AddCode("left", "System.Threading.Thread.Sleep(200); Context.Set<string>(\"result\", \"left\");")
            .AddEnd("left-end")
            .AddCode("right", "System.Threading.Thread.Sleep(10); Context.Set<string>(\"result\", \"right\");")
            .AddEnd("right-end")
            .Connect("start", "fanout")
            .Connect("fanout.left", "left")
            .Connect("left", "left-end")
            .Connect("fanout.right", "right")
            .Connect("right", "right-end")
            .Build();

        var subRun = await WorkflowRunner.RunWorkflow([], subWorkflow);

        Assert.NotNull(subRun);
        Assert.True(subRun.RunStatus == RunStatus.Success, subRun.Error);
        Assert.NotNull(subRun.OutputContext);

        var subOut = ContextObject.Deserialize(subRun.OutputContext);
        Assert.Equal("left", subOut.Get<string>("result"));

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", subWorkflowId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var parentRun = await WorkflowRunner.RunWorkflow([], parentWorkflow, subWorkflow);

        Assert.NotNull(parentRun);
        Assert.True(parentRun.RunStatus == RunStatus.Success, parentRun.Error);
        Assert.NotNull(parentRun.OutputContext);

        var parentOut = ContextObject.Deserialize(parentRun.OutputContext);
        Assert.Equal("left", parentOut.Get<string>("result"));
    }

    [Fact]
    public async Task Gosub_allows_nested_child_workflows()
    {
        var grandchildId = Guid.NewGuid();
        var grandchildWorkflow = new WorkflowBuilder()
            .WithId(grandchildId)
            .AddStart()
            .AddCode("grand", "Context.Set<string>(\"value\", \"grand\");")
            .AddEnd()
            .Connect("start", "grand")
            .Connect("grand", "end")
            .Build();

        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("seed", "Context.Set<string>(\"child\", \"yes\");")
            .AddGosub("grandchild", grandchildId)
            .AddCode("after", "Context.Set<string>(\"result\", Context.Get<string>(\"value\"));")
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "grandchild")
            .Connect("grandchild", "after")
            .Connect("after", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("child", childId)
            .AddEnd()
            .Connect("start", "child")
            .Connect("child", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow, grandchildWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("yes", outCtx.Get<string>("child"));
        Assert.Equal("grand", outCtx.Get<string>("value"));
        Assert.Equal("grand", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Gosub_run_multiple_gosubs_in_parallel_batch()
    {
        var subWorkflowId = Guid.NewGuid();
        var subWorkflow = new WorkflowBuilder()
            .WithId(subWorkflowId)
            .AddStart()
            .AddCode("sub", "System.Threading.Thread.Sleep(50); Context.Set<string>(\"output\", Context.Get<string>(\"items[0]\"));")
            .AddEnd()
            .Connect("start", "sub")
            .Connect("sub", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddBatch("batch", batchSize: 1, parallelBatches: 2, inputPath: "items")
            .AddGosub("gosub", subWorkflowId)
            .AddEnd()
            .Connect("start", "batch")
            .Connect("batch.process", "gosub")
            .Connect("batch.continue", "end")
            .Build();

        ContextObject ctx = [];
        var items = new ContextList { "alpha", "beta", "gamma", "delta" };
        ctx.Set("items", items);

        var run = await WorkflowRunner.RunWorkflow(ctx, parentWorkflow, subWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        var outputs = outCtx.Get<ContextList>("output");
        Assert.Equal(items.Count, outputs.Count);

        var values = outputs.Select(value => value?.ToString()).ToList();
        Assert.Contains("alpha", values);
        Assert.Contains("beta", values);
        Assert.Contains("gamma", values);
        Assert.Contains("delta", values);
    }

    [Fact]
    public async Task Gosub_fails_when_child_requires_missing_input()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart("start", true, WorkflowBuilder.CreateStringInput("input.name", false))
            .AddEnd()
            .Connect("start", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", childId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Start node mandatory path 'input.name' cannot be resolved.", run.Error);
    }

    [Fact]
    public async Task Gosub_allows_child_mandatory_input_when_provided()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart("start", true, WorkflowBuilder.CreateStringInput("input.name", false))
            .AddCode("use", "Context.Set<string>(\"result\", Context.Get<string>(\"input.name\"));")
            .AddEnd()
            .Connect("start", "use")
            .Connect("use", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddCode("seed", "Context.Set<string>(\"input.name\", \"Ada\");")
            .AddGosub("gosub", childId)
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("Ada", outCtx.Get<string>("input.name"));
        Assert.Equal("Ada", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Gosub_defaults_optional_child_input_when_missing()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart("start", true, WorkflowBuilder.CreateStringInput("input.name", true, "default"))
            .AddCode("use", "Context.Set<string>(\"result\", Context.Get<string>(\"input.name\"));")
            .AddEnd()
            .Connect("start", "use")
            .Connect("use", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", childId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("default", outCtx.Get<string>("input.name"));
        Assert.Equal("default", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Gosub_fails_when_child_code_throws()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("explode", "throw new System.InvalidOperationException(\"Boom\");")
            .AddEnd()
            .Connect("start", "explode")
            .Connect("explode", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", childId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Code node failed during execution.", run.Error);
        Assert.Contains("Boom", run.Error);
    }

    [Fact]
    public async Task Gosub_fails_when_workflow_id_missing()
    {
        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", null)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Gosub node workflow id must be set.", run.Error);
    }

    [Fact]
    public async Task Gosub_fails_when_child_workflow_missing()
    {
        var missingId = Guid.NewGuid();
        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", missingId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("GetWorkflow failed", run.Error);
    }

    [Fact]
    public async Task Gosub_fails_when_child_has_no_start()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddCode("work", "Context.Set<string>(\"value\", \"ok\");")
            .AddEnd()
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", childId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Must have exactly one start node.", run.Error);
    }

    [Fact]
    public async Task Gosub_fails_when_child_has_multiple_starts()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart("start1")
            .AddStart("start2")
            .AddEnd()
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", childId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Must have exactly one start node.", run.Error);
    }

    [Fact]
    public async Task Gosub_fails_when_output_count_invalid()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddEnd()
            .Connect("start", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub("gosub", childId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var gosubNode = parentWorkflow.Nodes.OfType<GosubNodeEntity>().Single();
        var extraOutput = new ConnectorEntity { Id = Guid.NewGuid(), Version = 1, Name = "extra" };
        gosubNode.Outputs = [.. gosubNode.Outputs, extraOutput];

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Node must have a single output but found 2.", run.Error);
    }

    [Fact]
    public async Task Gosub_self_referential_hits_run_node_limit()
    {
        var workflowId = Guid.NewGuid();
        var workflow = new WorkflowBuilder()
            .WithId(workflowId)
            .AddStart()
            .AddGosub("gosub", workflowId)
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);
        await repositoryService.UpsertSetting(new Setting
        {
            SettingId = Guid.NewGuid(),
            Name = "RunNodeLimit",
            DisplayName = "RunNodeLimit",
            SettingType = SettingType.Integer,
            UserEditable = false,
            ValueInteger = 20
        });

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var runId = await engine.CreateWorkflowRun(workflow.Id);
            var run = await engine.StartWorkflowRunAndWait(runId, []);

            Assert.NotNull(run);
            Assert.Equal(RunStatus.Failed, run.RunStatus);
            Assert.Equal("Hit run node limit of 20", run.Error);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Gosub_applies_input_mappings_without_parent()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("use", "if (Context.TryGet<string>(\"parentOnly\", out var _)) throw new System.InvalidOperationException(\"Parent leaked\"); Context.Set<string>(\"result\", Context.Get<string>(\"child.name\") + \":\" + Context.Get<string>(\"child.mode\"));")
            .AddEnd()
            .Connect("start", "use")
            .Connect("use", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddCode("seed", "Context.Set<string>(\"input.name\", \"Ada\"); Context.Set<string>(\"parentOnly\", \"nope\");")
            .AddGosub(
                "gosub",
                childId,
                applyInputMappings: true,
                inputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Input,
                        InputPath = "input.name",
                        OutputPath = "child.name",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    },
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Input,
                        InputPath = "input.mode",
                        OutputPath = "child.mode",
                        Optional = true,
                        EntryType = ContextEntryType.String,
                        EntryValue = "auto"
                    }
                ])
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("Ada", outCtx.Get<string>("child.name"));
        Assert.Equal("auto", outCtx.Get<string>("child.mode"));
        Assert.Equal("Ada:auto", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Gosub_applies_output_mappings_only()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("produce", "Context.Set<string>(\"publicValue\", \"ok\"); Context.Set<string>(\"secretValue\", \"hidden\");")
            .AddEnd()
            .Connect("start", "produce")
            .Connect("produce", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyOutputMappings: true,
                outputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Output,
                        InputPath = "publicValue",
                        OutputPath = "result.publicValue",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("ok", outCtx.Get<string>("result.publicValue"));
        Assert.False(outCtx.TryGet<string>("publicValue", out _));
        Assert.False(outCtx.TryGet<string>("secretValue", out _));
    }

    [Fact]
    public async Task Gosub_output_mapping_applies_without_end()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("produce", "Context.Set<string>(\"child.value\", \"done\");")
            .Connect("start", "produce")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyOutputMappings: true,
                outputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Output,
                        InputPath = "child.value",
                        OutputPath = "output.value",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddCode("after", "Context.Set<string>(\"result\", Context.Get<string>(\"output.value\"));")
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "after")
            .Connect("after", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("done", outCtx.Get<string>("output.value"));
        Assert.Equal("done", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Gosub_input_allows_empty_input_for_optional()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("use", "Context.Set<string>(\"result\", Context.Get<string>(\"child.fixedValue\"));")
            .AddEnd()
            .Connect("start", "use")
            .Connect("use", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyInputMappings: true,
                inputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Input,
                        InputPath = string.Empty,
                        OutputPath = "child.fixedValue",
                        Optional = true,
                        EntryType = ContextEntryType.String,
                        EntryValue = "fixed"
                    }
                ])
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("fixed", outCtx.Get<string>("child.fixedValue"));
        Assert.Equal("fixed", outCtx.Get<string>("result"));
    }

    [Fact]
    public async Task Gosub_output_mapping_skips_missing_input_path()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("produce", "Context.Set<string>(\"child.value\", \"ok\");")
            .AddEnd()
            .Connect("start", "produce")
            .Connect("produce", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyOutputMappings: true,
                outputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Output,
                        InputPath = "missing.value",
                        OutputPath = "output.value",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.False(outCtx.TryGet<string>("output.value", out _));
    }

    [Fact]
    public async Task Gosub_output_mapping_fails_on_empty_input_path()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddEnd()
            .Connect("start", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyOutputMappings: true,
                outputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Output,
                        InputPath = string.Empty,
                        OutputPath = "output.value",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Gosub output mapping input path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Gosub_output_mapping_fails_on_empty_output_path()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("produce", "Context.Set<string>(\"child.value\", \"ok\");")
            .AddEnd()
            .Connect("start", "produce")
            .Connect("produce", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyOutputMappings: true,
                outputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Output,
                        InputPath = "child.value",
                        OutputPath = string.Empty,
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Gosub output mapping output path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Gosub_input_mapping_fails_on_empty_input_path_when_required()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddEnd()
            .Connect("start", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyInputMappings: true,
                inputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Input,
                        InputPath = string.Empty,
                        OutputPath = "child.value",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Gosub node input path cannot be empty.", run.Error);
    }

    [Fact]
    public async Task Gosub_input_mapping_fails_on_invalid_output_path()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddEnd()
            .Connect("start", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddCode("seed", "Context.Set<string>(\"input.value\", \"ok\");")
            .AddGosub(
                "gosub",
                childId,
                applyInputMappings: true,
                inputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Input,
                        InputPath = "input.value",
                        OutputPath = "child.bad-key",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Gosub node input mapping could not set 'child.bad-key' into context.", run.Error);
    }

    [Fact]
    public async Task Gosub_output_mapping_applies_without_output_connection()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("produce", "Context.Set<string>(\"child.value\", \"done\");")
            .AddEnd()
            .Connect("start", "produce")
            .Connect("produce", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyOutputMappings: true,
                outputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Output,
                        InputPath = "child.value",
                        OutputPath = "output.value",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .Connect("start", "gosub")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("done", outCtx.Get<string>("output.value"));
    }

    [Fact]
    public async Task Gosub_output_mapping_uses_last_fanout_result()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddFanOut("fanout", ["left", "right"])
            .AddCode("left", "System.Threading.Thread.Sleep(200); Context.Set<string>(\"result\", \"left\");")
            .AddEnd("left-end")
            .AddCode("right", "System.Threading.Thread.Sleep(10); Context.Set<string>(\"result\", \"right\");")
            .AddEnd("right-end")
            .Connect("start", "fanout")
            .Connect("fanout.left", "left")
            .Connect("left", "left-end")
            .Connect("fanout.right", "right")
            .Connect("right", "right-end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyOutputMappings: true,
                outputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Output,
                        InputPath = "result",
                        OutputPath = "output.result",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.True(run.RunStatus == RunStatus.Success, run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.Equal("left", outCtx.Get<string>("output.result"));
    }

    [Fact]
    public async Task Gosub_does_not_apply_output_mapping_when_child_fails()
    {
        var childId = Guid.NewGuid();
        var childWorkflow = new WorkflowBuilder()
            .WithId(childId)
            .AddStart()
            .AddCode("explode", "Context.Set<string>(\"child.value\", \"done\"); throw new System.InvalidOperationException(\"Boom\");")
            .AddEnd()
            .Connect("start", "explode")
            .Connect("explode", "end")
            .Build();

        var parentWorkflow = new WorkflowBuilder()
            .WithId(Guid.NewGuid())
            .AddStart()
            .AddGosub(
                "gosub",
                childId,
                applyOutputMappings: true,
                outputMappings:
                [
                    new ContextEntryEntity
                    {
                        Id = Guid.NewGuid(),
                        Version = 1,
                        Purpose = ContextEntryPurpose.Output,
                        InputPath = "child.value",
                        OutputPath = "output.value",
                        Optional = false,
                        EntryType = ContextEntryType.String,
                        EntryValue = string.Empty
                    }
                ])
            .AddEnd()
            .Connect("start", "gosub")
            .Connect("gosub", "end")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], parentWorkflow, childWorkflow);

        Assert.NotNull(run);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Contains("Boom", run.Error);
        Assert.NotNull(run.OutputContext);

        var outCtx = ContextObject.Deserialize(run.OutputContext);
        Assert.False(outCtx.TryGet<string>("output.value", out _));
    }
}
