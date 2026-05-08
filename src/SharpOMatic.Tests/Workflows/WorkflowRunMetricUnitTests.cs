namespace SharpOMatic.Tests.Workflows;

public sealed class WorkflowRunMetricUnitTests
{
    [Fact]
    public async Task SuccessfulRunWritesOneWorkflowRunMetric()
    {
        var workflow = CreateSuccessfulWorkflow();
        using var provider = WorkflowRunner.BuildProvider();
        var repository = (TestRepositoryService)provider.GetRequiredService<IRepositoryService>();
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow.Id);
        var metric = await WaitForSingleWorkflowRunMetric(repository);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal(run.RunId, metric.RunId);
        Assert.Equal(workflow.Id, metric.WorkflowId);
        Assert.Equal("Workflow Metrics", metric.WorkflowName);
        Assert.Equal(workflow.Version, metric.WorkflowVersion);
        Assert.True(metric.Succeeded);
        Assert.Equal(RunStatus.Success, metric.RunStatus);
        Assert.Null(metric.ErrorMessage);
        Assert.Null(metric.ErrorType);
        Assert.Null(metric.FailedNodeEntityId);
        Assert.False(metric.IsConversationRun);
        Assert.True(metric.Duration >= 0);
    }

    [Fact]
    public async Task FailedRunWritesErrorAndFailedNodeSnapshotFields()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Failing Workflow")
            .AddStart()
            .AddCode("explode", "throw new System.InvalidOperationException(\"Boom\");")
            .Connect("start", "explode")
            .Build();
        var failedNode = workflow.Nodes.Single(node => node.Title == "explode");
        using var provider = WorkflowRunner.BuildProvider();
        var repository = (TestRepositoryService)provider.GetRequiredService<IRepositoryService>();
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow.Id);
        var metric = await WaitForSingleWorkflowRunMetric(repository);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.False(metric.Succeeded);
        Assert.Equal(RunStatus.Failed, metric.RunStatus);
        Assert.Contains("Boom", metric.ErrorMessage);
        Assert.NotNull(metric.ErrorType);
        Assert.Equal(failedNode.Id, metric.FailedNodeEntityId);
        Assert.Equal("explode", metric.FailedNodeTitle);
        Assert.Equal(NodeType.Code, metric.FailedNodeType);
    }

    [Fact]
    public async Task SuspendedConversationRunWritesSeparateNonFailureStatus()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Suspended Workflow")
            .EnableConversations()
            .AddStart()
            .AddSuspend("wait")
            .Connect("start", "wait")
            .Build();
        using var provider = WorkflowRunner.BuildProvider();
        var repository = (TestRepositoryService)provider.GetRequiredService<IRepositoryService>();
        await repository.UpsertWorkflow(workflow);
        var conversationId = $"conversation-{Guid.NewGuid():N}";

        var run = await RunConversation(provider, workflow.Id, conversationId);
        var metric = await WaitForSingleWorkflowRunMetric(repository);

        Assert.Equal(RunStatus.Suspended, run.RunStatus);
        Assert.False(metric.Succeeded);
        Assert.Equal(RunStatus.Suspended, metric.RunStatus);
        Assert.Null(metric.ErrorMessage);
        Assert.Null(metric.FailedNodeEntityId);
        Assert.True(metric.IsConversationRun);
        Assert.Equal(conversationId, metric.ConversationId);
        Assert.Equal(1, metric.TurnNumber);
    }

    [Fact]
    public async Task RunMetricPersistsAfterUnderlyingRunIsPruned()
    {
        var workflow = CreateSuccessfulWorkflow();
        using var provider = WorkflowRunner.BuildProvider();
        var repository = (TestRepositoryService)provider.GetRequiredService<IRepositoryService>();
        await repository.UpsertWorkflow(workflow);
        await repository.UpsertSetting(
            new Setting()
            {
                SettingId = Guid.NewGuid(),
                Name = "RunHistoryLimit",
                DisplayName = "Run History Limit",
                SettingType = SettingType.Integer,
                UserEditable = true,
                ValueInteger = 0,
            }
        );

        var run = await RunWorkflow(provider, workflow.Id);
        var metric = await WaitForSingleWorkflowRunMetric(repository);

        await WaitForCondition(() => repository.GetRun(run.RunId).ContinueWith(task => task.Result is null));
        Assert.Equal(run.RunId, metric.RunId);
        Assert.Contains(repository.GetWorkflowRunMetrics(), existing => existing.RunId == run.RunId);
    }

    [Fact]
    public async Task WorkflowRunMetricAggregatesModelCallTotals()
    {
        var model = CreateModel("openai");
        var workflow = CreateModelWorkflow(model.ModelId);
        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, UsageModelCaller>("openai"));
        var repository = (TestRepositoryService)provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repository, "openai", model);
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow.Id);
        var workflowMetric = await WaitForSingleWorkflowRunMetric(repository);
        var modelMetric = Assert.Single(repository.GetModelCallMetrics());

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal(run.RunId, modelMetric.RunId);
        Assert.Equal(1, workflowMetric.ModelCallCount);
        Assert.Equal(0, workflowMetric.ModelCallFailureCount);
        Assert.Equal(1000, workflowMetric.InputTokens);
        Assert.Equal(2000, workflowMetric.OutputTokens);
        Assert.Equal(3000, workflowMetric.TotalTokens);
        Assert.Equal(0.0055m, workflowMetric.TotalModelCost);
    }

    [Fact]
    public async Task WorkflowRunMetricsDashboardAggregatesRangeAllTimeBucketsAndTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using (var dbContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions())))
            await dbContext.Database.EnsureCreatedAsync();

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var workflowA = Guid.NewGuid();
        var workflowB = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var now = DateTime.UtcNow.Date.AddHours(12);
        var successRunId = Guid.NewGuid();
        var failedRunId = Guid.NewGuid();
        var suspendedRunId = Guid.NewGuid();
        var oldRunId = Guid.NewGuid();

        await repository.AppendModelCallMetric(CreateModelCallMetric(successRunId, now.AddHours(-2), true, 100, 50, 150, 0.01m));
        await repository.AppendModelCallMetric(CreateModelCallMetric(failedRunId, now.AddHours(-1), false, 200, 0, 200, null));

        await repository.AppendWorkflowRunMetric(CreateWorkflowRunMetric(successRunId, workflowA, "Workflow A", now.AddHours(-2), RunStatus.Success, 100));
        await repository.AppendWorkflowRunMetric(CreateWorkflowRunMetric(failedRunId, workflowA, "Workflow A", now.AddHours(-1), RunStatus.Failed, 400, nodeId));
        await repository.AppendWorkflowRunMetric(CreateWorkflowRunMetric(suspendedRunId, workflowA, "Workflow A", now, RunStatus.Suspended, 900, conversationId: "conversation-1"));
        await repository.AppendWorkflowRunMetric(CreateWorkflowRunMetric(oldRunId, workflowB, "Workflow B", now.AddDays(-10), RunStatus.Success, 1600));

        await repository.AppendWorkflowRunMetric(CreateWorkflowRunMetric(successRunId, workflowA, "Workflow A", now.AddHours(-2), RunStatus.Success, 100));

        var dashboard = await repository.GetWorkflowRunMetricsDashboard(
            new WorkflowRunMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Hour, WorkflowRunMetricScope.All, null, null, 0, 2)
        );

        Assert.Equal(3, dashboard.Totals.TotalRuns);
        Assert.Equal(1, dashboard.Totals.SuccessfulRuns);
        Assert.Equal(1, dashboard.Totals.FailedRuns);
        Assert.Equal(1, dashboard.Totals.SuspendedRuns);
        Assert.Equal(1 / 3d, dashboard.Totals.FailureRate);
        Assert.Equal(900, dashboard.Totals.P95Duration);
        Assert.Equal(2, dashboard.Totals.ModelCallCount);
        Assert.Equal(1, dashboard.Totals.ModelCallFailureCount);
        Assert.Equal(350, dashboard.Totals.TotalTokens);
        Assert.Equal(0.01m, dashboard.Totals.TotalModelCost);
        Assert.Equal(24, dashboard.TimeBuckets.Count);
        Assert.Contains(dashboard.TimeBuckets, bucket => bucket.FailedRuns == 1);
        Assert.Contains(dashboard.TimeBuckets, bucket => bucket.SuspendedRuns == 1);
        Assert.Single(dashboard.WorkflowBreakdown);
        Assert.Equal("Workflow A", dashboard.WorkflowBreakdown[0].Name);
        Assert.Single(dashboard.Failures);
        Assert.Equal("Boom", dashboard.Failures[0].ErrorMessage);
        Assert.Equal(nodeId, dashboard.Failures[0].FailedNodeEntityId);
        Assert.Equal(3, dashboard.RecentRunsTotal);
        Assert.Equal(2, dashboard.RecentRuns.Count);
        Assert.Equal(suspendedRunId, dashboard.RecentRuns[0].RunId);
        Assert.Equal(suspendedRunId, dashboard.SlowestRuns[0].RunId);

        var workflowMasterDashboard = await repository.GetWorkflowRunMetricsDashboard(
            new WorkflowRunMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Hour, WorkflowRunMetricScope.Workflow, null, "Workflow A", 0, 25)
        );
        var workflowMasterItem = Assert.Single(workflowMasterDashboard.MasterItems);
        Assert.Equal("Workflow A", workflowMasterItem.Name);

        var workflowScopedDashboard = await repository.GetWorkflowRunMetricsDashboard(
            new WorkflowRunMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Hour, WorkflowRunMetricScope.Workflow, workflowMasterItem.Key, null, 0, 25)
        );
        Assert.Equal("Workflow A", workflowScopedDashboard.ScopeName);
        Assert.Equal(3, workflowScopedDashboard.Totals.TotalRuns);

        var errorMasterDashboard = await repository.GetWorkflowRunMetricsDashboard(
            new WorkflowRunMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Hour, WorkflowRunMetricScope.Error, null, null, 0, 25)
        );
        var errorMasterItem = Assert.Single(errorMasterDashboard.MasterItems);
        Assert.Contains("Boom", errorMasterItem.Name);

        var errorScopedDashboard = await repository.GetWorkflowRunMetricsDashboard(
            new WorkflowRunMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Hour, WorkflowRunMetricScope.Error, errorMasterItem.Key, null, 0, 25)
        );
        Assert.Equal(errorMasterItem.Name, errorScopedDashboard.ScopeName);
        Assert.Equal(1, errorScopedDashboard.Totals.TotalRuns);
        Assert.Equal(1, errorScopedDashboard.Totals.FailedRuns);

        var allTimeDashboard = await repository.GetWorkflowRunMetricsDashboard(
            new WorkflowRunMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, WorkflowRunMetricScope.All, null, null, 0, 25, AllTime: true)
        );

        Assert.Equal(4, allTimeDashboard.Totals.TotalRuns);
        Assert.Equal(2, allTimeDashboard.Totals.SuccessfulRuns);
        Assert.Equal(GetBucketStart(now.AddDays(-10)), allTimeDashboard.Start);
        Assert.Equal(GetBucketStart(now).AddDays(1), allTimeDashboard.End);
    }

    private static WorkflowEntity CreateSuccessfulWorkflow()
    {
        return new WorkflowBuilder()
            .WithName("Workflow Metrics")
            .AddStart()
            .AddCode("set value", "Context.Set<string>(\"output.value\", \"ok\");")
            .Connect("start", "set value")
            .Build();
    }

    private static WorkflowEntity CreateModelWorkflow(Guid? modelId)
    {
        var workflow = new WorkflowBuilder()
            .WithName("Model Workflow")
            .AddStart()
            .AddModelCall("model")
            .AddEnd()
            .Connect("start", "model")
            .Connect("model", "end")
            .Build();

        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(node => node.Title == "model"));
        node.ModelId = modelId;
        node.Prompt = "Say hello";
        node.TextOutputPath = "output.text";
        return workflow;
    }

    private static async Task<Run> RunWorkflow(ServiceProvider provider, Guid workflowId)
    {
        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            return await engine.StartWorkflowRunAndWait(workflowId, []);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static async Task<Run> RunConversation(ServiceProvider provider, Guid workflowId, string conversationId)
    {
        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            return await engine.StartOrResumeConversationAndWait(workflowId, conversationId);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static async Task<WorkflowRunMetric> WaitForSingleWorkflowRunMetric(TestRepositoryService repository)
    {
        WorkflowRunMetric? metric = null;
        await WaitForCondition(() =>
        {
            var metrics = repository.GetWorkflowRunMetrics();
            metric = metrics.SingleOrDefault();
            return Task.FromResult(metric is not null);
        });

        return metric!;
    }

    private static async Task WaitForCondition(Func<Task<bool>> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate())
                return;

            await Task.Delay(25);
        }

        Assert.True(await predicate(), "Condition was not met before timeout.");
    }

    private static async Task SeedModelCallMetadata(IRepositoryService repository, string configId, Model model)
    {
        await repository.UpsertConnectorConfig(
            new ConnectorConfig()
            {
                Version = 1,
                ConfigId = configId,
                DisplayName = configId,
                Description = configId,
                AuthModes = [],
            }
        );

        await repository.UpsertConnector(
            new Connector()
            {
                Version = 1,
                ConnectorId = model.ConnectorId!.Value,
                Name = $"{configId}-connector",
                Description = $"{configId}-connector",
                ConfigId = configId,
                AuthenticationModeId = string.Empty,
                FieldValues = [],
            }
        );

        await repository.UpsertModelConfig(
            new ModelConfig()
            {
                Version = 1,
                ConfigId = model.ConfigId,
                DisplayName = $"{configId}-model",
                Description = $"{configId}-model",
                ConnectorConfigId = configId,
                IsCustom = false,
                Information =
                [
                    new ModelInformation()
                    {
                        Name = "InputPrice",
                        DisplayName = "Input Price per Million",
                        Type = FieldDescriptorType.Double,
                        Value = 1.5,
                    },
                    new ModelInformation()
                    {
                        Name = "OutputPrice",
                        DisplayName = "Output Price per Million",
                        Type = FieldDescriptorType.Double,
                        Value = 2.0,
                    },
                ],
                Capabilities = [],
                ParameterFields = [],
            }
        );

        await repository.UpsertModel(model);
    }

    private static Model CreateModel(string connectorConfigId)
    {
        return new Model()
        {
            Version = 1,
            ModelId = Guid.NewGuid(),
            Name = $"{connectorConfigId}-model",
            Description = $"{connectorConfigId}-model",
            ConfigId = $"{connectorConfigId}-model-config",
            ConnectorId = Guid.NewGuid(),
            CustomCapabilities = [],
            ParameterValues = [],
        };
    }

    private static ModelCallMetric CreateModelCallMetric(Guid runId, DateTime created, bool succeeded, long inputTokens, long outputTokens, long totalTokens, decimal? totalCost)
    {
        return new ModelCallMetric()
        {
            Id = Guid.NewGuid(),
            Created = created,
            Duration = 10,
            Succeeded = succeeded,
            ErrorType = succeeded ? null : typeof(InvalidOperationException).FullName,
            ErrorMessage = succeeded ? null : "model failed",
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "Workflow",
            RunId = runId,
            NodeEntityId = Guid.NewGuid(),
            NodeTitle = "model",
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            TotalCost = totalCost,
        };
    }

    private static WorkflowRunMetric CreateWorkflowRunMetric(
        Guid runId,
        Guid workflowId,
        string workflowName,
        DateTime created,
        RunStatus status,
        long duration,
        Guid? failedNodeId = null,
        string? conversationId = null
    )
    {
        var started = created.AddMilliseconds(-duration);
        return new WorkflowRunMetric()
        {
            Id = Guid.NewGuid(),
            Created = created,
            Started = started,
            Finished = created,
            Duration = duration,
            RunId = runId,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            WorkflowVersion = 1,
            Succeeded = status == RunStatus.Success,
            RunStatus = status,
            ErrorType = status == RunStatus.Failed ? typeof(InvalidOperationException).FullName : null,
            ErrorMessage = status == RunStatus.Failed ? "Boom" : null,
            FailedNodeEntityId = failedNodeId,
            FailedNodeTitle = failedNodeId.HasValue ? "explode" : null,
            FailedNodeType = failedNodeId.HasValue ? NodeType.Code : null,
            ConversationId = conversationId,
            TurnNumber = conversationId is null ? null : 1,
            IsConversationRun = conversationId is not null,
        };
    }

    private static DateTime GetBucketStart(DateTime value) => new(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Utc);

    private sealed class UsageModelCaller : IModelCaller
    {
        public Task<ModelCallResult> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            return Task.FromResult(
                new ModelCallResult()
                {
                    Chat = [],
                    Responses = [new ChatMessage(ChatRole.Assistant, [new TextContent("Hello")])],
                    ResultValue = "Hello",
                    ProviderModelName = "provider-model",
                    Usage = new UsageDetails()
                    {
                        InputTokenCount = 1000,
                        OutputTokenCount = 2000,
                        TotalTokenCount = 3000,
                    },
                }
            );
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<SharpOMaticDbContext> options) : IDbContextFactory<SharpOMaticDbContext>
    {
        public SharpOMaticDbContext CreateDbContext() => new(options, Options.Create(new SharpOMaticDbOptions()));
    }
}
