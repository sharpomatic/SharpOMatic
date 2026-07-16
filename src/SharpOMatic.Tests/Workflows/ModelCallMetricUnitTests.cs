namespace SharpOMatic.Tests.Workflows;

public class ModelCallMetricUnitTests
{
    [Fact]
    public async Task SuccessfulModelCallWritesMetricWithUsageAndCost()
    {
        var model = CreateModel("openai");
        var workflow = CreateWorkflow(model.ModelId);
        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, UsageModelCaller>("openai"));
        var repository = (TestRepositoryService)provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repository, "openai", model);
        await repository.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engine.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);
            var metric = Assert.Single(repository.GetModelCallMetrics());
            Assert.True(metric.Succeeded);
            Assert.NotEqual(Guid.Empty, metric.LogicalCallId);
            Assert.Equal(1, metric.AttemptNumber);
            Assert.Null(metric.FailureCategory);
            Assert.Null(metric.ProviderStatusCode);
            Assert.Equal(workflow.Id, metric.WorkflowId);
            Assert.Equal("Metric Workflow", metric.WorkflowName);
            Assert.Equal(run.RunId, metric.RunId);
            Assert.Null(metric.ConversationId);
            Assert.Equal("model", metric.NodeTitle);
            Assert.Equal(model.ModelId, metric.ModelId);
            Assert.Equal(model.Name, metric.ModelName);
            Assert.Equal("openai-model-config", metric.ModelConfigId);
            Assert.Equal("openai-model", metric.ModelConfigName);
            Assert.Equal(model.ConnectorId, metric.ConnectorId);
            Assert.Equal("openai-connector", metric.ConnectorName);
            Assert.Equal("openai", metric.ConnectorConfigId);
            Assert.Equal("openai", metric.ConnectorConfigName);
            Assert.Equal("provider-model", metric.ProviderModelName);
            Assert.Equal(1000, metric.InputTokens);
            Assert.Equal(2000, metric.OutputTokens);
            Assert.Equal(3000, metric.TotalTokens);
            Assert.Equal(0.0015m, metric.InputCost);
            Assert.Equal(0.004m, metric.OutputCost);
            Assert.Equal(0.0055m, metric.TotalCost);
            Assert.True(metric.Duration >= 0);
            Assert.Null(metric.ErrorMessage);
            Assert.Null(metric.ErrorType);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task ModelCallFailureBeforeModelLoadWritesMetric()
    {
        var workflow = CreateWorkflow(modelId: null);
        using var provider = WorkflowRunner.BuildProvider();
        var repository = (TestRepositoryService)provider.GetRequiredService<IRepositoryService>();
        await repository.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engine.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Failed, run.RunStatus);
            var metric = Assert.Single(repository.GetModelCallMetrics());
            Assert.False(metric.Succeeded);
            Assert.NotEqual(Guid.Empty, metric.LogicalCallId);
            Assert.Equal(1, metric.AttemptNumber);
            Assert.Equal(ModelFallbackFailureCategory.Configuration, metric.FailureCategory);
            Assert.Equal(workflow.Id, metric.WorkflowId);
            Assert.Equal("Metric Workflow", metric.WorkflowName);
            Assert.Equal(run.RunId, metric.RunId);
            Assert.Equal("model", metric.NodeTitle);
            Assert.Null(metric.ModelId);
            Assert.Equal("No model selected", metric.ErrorMessage);
            Assert.Equal(typeof(SharpOMaticException).FullName, metric.ErrorType);
            Assert.True(metric.Duration >= 0);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task ModelCallerFailureWritesMetricWithSnapshots()
    {
        var model = CreateModel("openai");
        var workflow = CreateWorkflow(model.ModelId);
        using var provider = WorkflowRunner.BuildProvider(services => services.AddKeyedScoped<IModelCaller, FailingModelCaller>("openai"));
        var repository = (TestRepositoryService)provider.GetRequiredService<IRepositoryService>();
        await SeedModelCallMetadata(repository, "openai", model);
        await repository.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engine.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Failed, run.RunStatus);
            var metric = Assert.Single(repository.GetModelCallMetrics());
            Assert.False(metric.Succeeded);
            Assert.Equal(model.ModelId, metric.ModelId);
            Assert.Equal(model.Name, metric.ModelName);
            Assert.Equal(model.ConnectorId, metric.ConnectorId);
            Assert.Equal("openai-connector", metric.ConnectorName);
            Assert.Equal("provider failed", metric.ErrorMessage);
            Assert.Equal(typeof(InvalidOperationException).FullName, metric.ErrorType);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task DeletingWorkflowDoesNotDeleteModelCallMetric()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions()));
        await dbContext.Database.EnsureCreatedAsync();

        var workflowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var metricId = Guid.NewGuid();

        dbContext.Workflows.Add(
            new Workflow()
            {
                WorkflowId = workflowId,
                Version = 1,
                Named = "Deleted Workflow",
                Description = string.Empty,
                Nodes = "[]",
                Connections = "[]",
            }
        );
        dbContext.Runs.Add(
            new Run()
            {
                RunId = runId,
                WorkflowId = workflowId,
                Created = DateTime.UtcNow,
                RunStatus = RunStatus.Success,
            }
        );
        dbContext.ModelCallMetrics.Add(
            new ModelCallMetric()
            {
                Id = metricId,
                Created = DateTime.UtcNow,
                Succeeded = true,
                WorkflowId = workflowId,
                WorkflowName = "Deleted Workflow",
                RunId = runId,
                NodeEntityId = Guid.NewGuid(),
                NodeTitle = "model",
            }
        );
        await dbContext.SaveChangesAsync();

        var workflow = await dbContext.Workflows.SingleAsync(workflow => workflow.WorkflowId == workflowId);
        dbContext.Workflows.Remove(workflow);
        await dbContext.SaveChangesAsync();

        Assert.True(await dbContext.ModelCallMetrics.AnyAsync(metric => metric.Id == metricId));
        Assert.False(await dbContext.Runs.AnyAsync(run => run.RunId == runId));
    }

    [Fact]
    public async Task ModelCallMetricsDashboardAggregatesSelectedWorkflow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using (var dbContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions())))
            await dbContext.Database.EnsureCreatedAsync();

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var workflowA = Guid.NewGuid();
        var workflowB = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var now = DateTime.UtcNow.Date.AddHours(12);

        await repository.AppendModelCallMetric(
            new ModelCallMetric()
            {
                Id = Guid.NewGuid(),
                Created = now.AddHours(-2),
                Duration = 100,
                Succeeded = true,
                WorkflowId = workflowA,
                WorkflowName = "Workflow A",
                RunId = Guid.NewGuid(),
                NodeEntityId = Guid.NewGuid(),
                NodeTitle = "model one",
                ConnectorId = connectorId,
                ConnectorName = "OpenAI",
                ModelId = modelId,
                ModelName = "GPT",
                InputTokens = 100,
                OutputTokens = 50,
                TotalTokens = 150,
                TotalCost = 0.01m,
            }
        );
        await repository.AppendModelCallMetric(
            new ModelCallMetric()
            {
                Id = Guid.NewGuid(),
                Created = now.AddHours(-1),
                Duration = 400,
                Succeeded = false,
                ErrorType = typeof(InvalidOperationException).FullName,
                ErrorMessage = "provider failed",
                WorkflowId = workflowA,
                WorkflowName = "Workflow A",
                RunId = Guid.NewGuid(),
                NodeEntityId = Guid.NewGuid(),
                NodeTitle = "model two",
                ConnectorId = connectorId,
                ConnectorName = "OpenAI",
                ModelId = modelId,
                ModelName = "GPT",
                InputTokens = 200,
                OutputTokens = 0,
                TotalTokens = 200,
            }
        );
        await repository.AppendModelCallMetric(
            new ModelCallMetric()
            {
                Id = Guid.NewGuid(),
                Created = now,
                Duration = 900,
                Succeeded = true,
                WorkflowId = workflowB,
                WorkflowName = "Workflow B",
                RunId = Guid.NewGuid(),
                NodeEntityId = Guid.NewGuid(),
                NodeTitle = "model",
                ConnectorId = connectorId,
                ConnectorName = "OpenAI",
                ModelId = modelId,
                ModelName = "GPT",
                InputTokens = 300,
                OutputTokens = 100,
                TotalTokens = 400,
                TotalCost = 0.02m,
            }
        );

        var dashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Workflow, "name:Workflow A", null, 0, 25)
        );

        Assert.Equal("Workflow A", dashboard.ScopeName);
        Assert.Equal(2, dashboard.MasterItems.Count);
        Assert.Equal(2, dashboard.Totals.TotalCalls);
        Assert.Equal(1, dashboard.Totals.SuccessfulCalls);
        Assert.Equal(1, dashboard.Totals.FailedCalls);
        Assert.Equal(350, dashboard.Totals.TotalTokens);
        Assert.Equal(0.01m, dashboard.Totals.TotalCost);
        Assert.Equal(1, dashboard.Totals.PricedCalls);
        Assert.Equal(1, dashboard.Totals.UnpricedCalls);
        Assert.Equal(0.5, dashboard.Totals.FailureRate);
        Assert.Equal(400, dashboard.Totals.P95Duration);
        Assert.Single(dashboard.Failures);
        Assert.Equal("provider failed", dashboard.Failures[0].ErrorMessage);
        Assert.Equal(2, dashboard.RecentCallsTotal);
        Assert.Equal(2, dashboard.RecentCalls.Count);
        Assert.Equal(2, dashboard.NodeBreakdown.Count);
        Assert.Single(dashboard.TimeBuckets);
        Assert.Equal(2, dashboard.TimeBuckets[0].TotalCalls);
    }

    [Fact]
    public async Task ModelCallMetricsDashboardScopesUseSnapshotNamesWhenEntitiesAreRenamed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using (var dbContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions())))
            await dbContext.Database.EnsureCreatedAsync();

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var workflowId = Guid.NewGuid();
        var connectorId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var now = DateTime.UtcNow.Date.AddHours(12);

        await repository.AppendModelCallMetric(CreateModelCallMetric(now.AddHours(-1), workflowId, "Old Workflow", connectorId, "Old Connector", modelId, "Old Model", 100, 0.01m));
        await repository.AppendModelCallMetric(CreateModelCallMetric(now, workflowId, "New Workflow", connectorId, "New Connector", modelId, "New Model", 200, 0.02m));

        var workflowMasterDashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Workflow, null, null, 0, 25)
        );
        var oldWorkflowItem = Assert.Single(workflowMasterDashboard.MasterItems, item => item.Name == "Old Workflow");
        var newWorkflowItem = Assert.Single(workflowMasterDashboard.MasterItems, item => item.Name == "New Workflow");
        Assert.Equal("name:Old Workflow", oldWorkflowItem.Key);
        Assert.Equal("name:New Workflow", newWorkflowItem.Key);

        var oldWorkflowDashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Workflow, oldWorkflowItem.Key, null, 0, 25)
        );
        Assert.Equal("Old Workflow", oldWorkflowDashboard.ScopeName);
        Assert.Equal(1, oldWorkflowDashboard.Totals.TotalCalls);
        Assert.Single(oldWorkflowDashboard.RecentCalls);
        Assert.Equal("Old Workflow", oldWorkflowDashboard.RecentCalls[0].WorkflowName);

        var connectorMasterDashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Connector, null, null, 0, 25)
        );
        var oldConnectorItem = Assert.Single(connectorMasterDashboard.MasterItems, item => item.Name == "Old Connector");
        var newConnectorItem = Assert.Single(connectorMasterDashboard.MasterItems, item => item.Name == "New Connector");
        Assert.Equal("name:Old Connector", oldConnectorItem.Key);
        Assert.Equal("name:New Connector", newConnectorItem.Key);

        var oldConnectorDashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Connector, oldConnectorItem.Key, null, 0, 25)
        );
        Assert.Equal("Old Connector", oldConnectorDashboard.ScopeName);
        Assert.Equal(1, oldConnectorDashboard.Totals.TotalCalls);
        Assert.Single(oldConnectorDashboard.RecentCalls);
        Assert.Equal("Old Connector", oldConnectorDashboard.RecentCalls[0].ConnectorName);

        var modelMasterDashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Model, null, null, 0, 25)
        );
        var oldModelItem = Assert.Single(modelMasterDashboard.MasterItems, item => item.Name == "Old Model");
        var newModelItem = Assert.Single(modelMasterDashboard.MasterItems, item => item.Name == "New Model");
        Assert.Equal("name:Old Model", oldModelItem.Key);
        Assert.Equal("name:New Model", newModelItem.Key);

        var oldModelDashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Model, oldModelItem.Key, null, 0, 25)
        );
        Assert.Equal("Old Model", oldModelDashboard.ScopeName);
        Assert.Equal(1, oldModelDashboard.Totals.TotalCalls);
        Assert.Single(oldModelDashboard.RecentCalls);
        Assert.Equal("Old Model", oldModelDashboard.RecentCalls[0].ModelName);
    }

    [Fact]
    public async Task ModelCallMetricsDashboardDerivesFallbackRecoveryAndLogicalCallGroups()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using (var dbContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions())))
            await dbContext.Database.EnsureCreatedAsync();

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var now = DateTime.UtcNow.Date.AddHours(12);
        var workflowId = Guid.NewGuid();
        var logicalCallId = Guid.NewGuid();
        var primaryConnectorId = Guid.NewGuid();
        var fallbackConnectorId = Guid.NewGuid();
        var primaryModelId = Guid.NewGuid();
        var fallbackModelId = Guid.NewGuid();

        var failedPrimary = CreateModelCallMetric(now.AddMinutes(-3), workflowId, "Workflow", primaryConnectorId, "Primary connector", primaryModelId, "Primary model", 0, 0);
        failedPrimary.LogicalCallId = logicalCallId;
        failedPrimary.AttemptNumber = 1;
        failedPrimary.Succeeded = false;
        failedPrimary.Duration = 100;
        failedPrimary.FailureCategory = ModelFallbackFailureCategory.ProviderUnavailable;
        failedPrimary.ProviderStatusCode = 503;
        failedPrimary.ErrorMessage = "unavailable";
        await repository.AppendModelCallMetric(failedPrimary);

        var successfulFallback = CreateModelCallMetric(now.AddMinutes(-2), workflowId, "Workflow", fallbackConnectorId, "Fallback connector", fallbackModelId, "Fallback model", 100, 0.02m);
        successfulFallback.LogicalCallId = logicalCallId;
        successfulFallback.AttemptNumber = 2;
        successfulFallback.Duration = 200;
        await repository.AppendModelCallMetric(successfulFallback);

        var unrecovered = CreateModelCallMetric(now.AddMinutes(-1), workflowId, "Workflow", primaryConnectorId, "Primary connector", primaryModelId, "Primary model", 0, 0);
        unrecovered.Succeeded = false;
        unrecovered.Duration = 50;
        unrecovered.FailureCategory = ModelFallbackFailureCategory.RateLimited;
        unrecovered.ProviderStatusCode = 429;
        unrecovered.ErrorMessage = "rate limited";
        await repository.AppendModelCallMetric(unrecovered);

        var dashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.All, null, null, 0, 25)
        );

        Assert.Equal(3, dashboard.Totals.TotalCalls);
        Assert.Equal(2, dashboard.Totals.LogicalCalls);
        Assert.Equal(1, dashboard.Totals.CallsRequiringFallback);
        Assert.Equal(1, dashboard.Totals.RecoveredCalls);
        Assert.Equal(1, dashboard.Totals.UnrecoveredCalls);
        Assert.Equal(1, dashboard.Totals.FallbackRecoveryRate);
        Assert.Equal(0.5, dashboard.Totals.LogicalFailureRate);
        Assert.Equal(2d / 3d, dashboard.Totals.FailureRate, 10);

        Assert.Equal(2, dashboard.RecentLogicalCallsTotal);
        var recoveredCall = Assert.Single(dashboard.RecentLogicalCalls, call => call.LogicalCallId == logicalCallId);
        Assert.True(recoveredCall.Succeeded);
        Assert.True(recoveredCall.RecoveredByFallback);
        Assert.Equal(2, recoveredCall.AttemptCount);
        Assert.Equal(300, recoveredCall.Duration);
        Assert.Equal([1, 2], recoveredCall.Attempts.Select(attempt => attempt.AttemptNumber));

        Assert.Equal(2, dashboard.FailureCategories.Count);
        Assert.Contains(dashboard.FailureCategories, group => group.Category == ModelFallbackFailureCategory.ProviderUnavailable && group.ProviderStatusCode == 503);
        Assert.Contains(dashboard.FailureCategories, group => group.Category == ModelFallbackFailureCategory.RateLimited && group.ProviderStatusCode == 429);

        var primaryBreakdown = Assert.Single(dashboard.ConnectorBreakdown, item => item.Name == "Primary connector");
        Assert.Equal(2, primaryBreakdown.PrimaryAttempts);
        Assert.Equal(0, primaryBreakdown.FallbackAttempts);
        var fallbackBreakdown = Assert.Single(dashboard.ConnectorBreakdown, item => item.Name == "Fallback connector");
        Assert.Equal(0, fallbackBreakdown.PrimaryAttempts);
        Assert.Equal(1, fallbackBreakdown.FallbackAttempts);
        Assert.Equal(1, fallbackBreakdown.SuccessfulFallbackAttempts);

        var bucket = Assert.Single(dashboard.TimeBuckets);
        Assert.Equal(2, bucket.PrimaryAttempts);
        Assert.Equal(1, bucket.FallbackAttempts);
        Assert.Equal(1, bucket.SuccessfulFallbackAttempts);
        Assert.Equal(1, bucket.RecoveredCalls);
        Assert.Equal(1, bucket.UnrecoveredCalls);

        var primaryConnectorDashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Connector, "name:Primary connector", null, 0, 25)
        );
        Assert.Equal(2, primaryConnectorDashboard.Totals.TotalCalls);
        Assert.Equal(2, primaryConnectorDashboard.Totals.LogicalCalls);
        Assert.Equal(1, primaryConnectorDashboard.Totals.RecoveredCalls);

        var fallbackConnectorDashboard = await repository.GetModelCallMetricsDashboard(
            new ModelCallMetricsDashboardRequest(now.Date, now.Date.AddDays(1), ModelCallMetricBucket.Day, ModelCallMetricScope.Connector, "name:Fallback connector", null, 0, 25)
        );
        Assert.Equal(1, fallbackConnectorDashboard.Totals.TotalCalls);
        Assert.Equal(1, fallbackConnectorDashboard.Totals.LogicalCalls);
        Assert.Equal(1, fallbackConnectorDashboard.Totals.RecoveredCalls);
        Assert.Equal(2, Assert.Single(fallbackConnectorDashboard.RecentLogicalCalls).Attempts.Count);
    }

    private static WorkflowEntity CreateWorkflow(Guid? modelId)
    {
        var workflow = new WorkflowBuilder().WithName("Metric Workflow").AddStart().AddModelCall("model").AddEnd().Connect("start", "model").Connect("model", "end").Build();

        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(node => node.Title == "model"));
        node.ModelId = modelId;
        node.Prompt = "Say hello";
        node.TextOutputPath = "output.text";
        return workflow;
    }

    private static ModelCallMetric CreateModelCallMetric(
        DateTime created,
        Guid workflowId,
        string workflowName,
        Guid connectorId,
        string connectorName,
        Guid modelId,
        string modelName,
        long totalTokens,
        decimal totalCost
    )
    {
        return new ModelCallMetric()
        {
            Id = Guid.NewGuid(),
            Created = created,
            Duration = 100,
            Succeeded = true,
            WorkflowId = workflowId,
            WorkflowName = workflowName,
            RunId = Guid.NewGuid(),
            NodeEntityId = Guid.NewGuid(),
            NodeTitle = "model",
            ConnectorId = connectorId,
            ConnectorName = connectorName,
            ModelId = modelId,
            ModelName = modelName,
            TotalTokens = totalTokens,
            TotalCost = totalCost,
        };
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

    private sealed class FailingModelCaller : IModelCaller
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
            throw new InvalidOperationException("provider failed");
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<SharpOMaticDbContext> options) : IDbContextFactory<SharpOMaticDbContext>
    {
        public SharpOMaticDbContext CreateDbContext() => new(options, Options.Create(new SharpOMaticDbOptions()));
    }
}
