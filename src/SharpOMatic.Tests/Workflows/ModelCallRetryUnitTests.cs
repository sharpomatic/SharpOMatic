namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallRetryUnitTests
{
    [Theory]
    [InlineData(ModelFallbackFailureCategory.RateLimited, true)]
    [InlineData(ModelFallbackFailureCategory.ProviderUnavailable, true)]
    [InlineData(ModelFallbackFailureCategory.Timeout, true)]
    [InlineData(ModelFallbackFailureCategory.Network, true)]
    [InlineData(ModelFallbackFailureCategory.InvalidRequest, false)]
    [InlineData(ModelFallbackFailureCategory.Authentication, false)]
    [InlineData(ModelFallbackFailureCategory.Configuration, false)]
    [InlineData(ModelFallbackFailureCategory.Cancellation, false)]
    public void Default_policy_retries_only_transient_categories(ModelFallbackFailureCategory category, bool expectedRetry)
    {
        var failure = new ModelFallbackFailure(category, StatusCode: null, RetryAfter: null, IsTransient: expectedRetry);

        (var decision, _) = ModelRetryPolicy.Decide(failure, tryNumber: 1, new ModelRetryOptions());

        Assert.Equal(expectedRetry, decision.ShouldRetry);
    }

    [Fact]
    public void Default_policy_allows_two_retries_then_stops()
    {
        var failure = new ModelFallbackFailure(ModelFallbackFailureCategory.ProviderUnavailable, 503, RetryAfter: null, IsTransient: true);
        var options = new ModelRetryOptions();

        Assert.True(ModelRetryPolicy.Decide(failure, 1, options).Decision.ShouldRetry);
        Assert.True(ModelRetryPolicy.Decide(failure, 2, options).Decision.ShouldRetry);
        Assert.False(ModelRetryPolicy.Decide(failure, 3, options).Decision.ShouldRetry);
    }

    [Fact]
    public void Retry_after_is_honoured_and_clamped_to_the_maximum_delay()
    {
        var options = new ModelRetryOptions { MaxDelay = TimeSpan.FromSeconds(10) };

        var shortWait = new ModelFallbackFailure(ModelFallbackFailureCategory.RateLimited, 429, TimeSpan.FromSeconds(4), IsTransient: true);
        Assert.Equal(TimeSpan.FromSeconds(4), ModelRetryPolicy.CalculateDelay(shortWait, 1, options));

        var longWait = new ModelFallbackFailure(ModelFallbackFailureCategory.RateLimited, 429, TimeSpan.FromMinutes(5), IsTransient: true);
        Assert.Equal(TimeSpan.FromSeconds(10), ModelRetryPolicy.CalculateDelay(longWait, 1, options));
    }

    [Fact]
    public void Backoff_grows_between_tries_and_never_exceeds_the_maximum_delay()
    {
        var failure = new ModelFallbackFailure(ModelFallbackFailureCategory.ProviderUnavailable, 503, RetryAfter: null, IsTransient: true);
        var options = new ModelRetryOptions
        {
            BaseDelay = TimeSpan.FromSeconds(1),
            BackoffFactor = 2,
            MaxDelay = TimeSpan.FromSeconds(30),
            JitterFactor = 0,
        };

        Assert.Equal(TimeSpan.FromSeconds(1), ModelRetryPolicy.CalculateDelay(failure, 1, options));
        Assert.Equal(TimeSpan.FromSeconds(2), ModelRetryPolicy.CalculateDelay(failure, 2, options));
        Assert.Equal(TimeSpan.FromSeconds(30), ModelRetryPolicy.CalculateDelay(failure, 20, options));
    }

    [Fact]
    public async Task Transient_failure_is_retried_on_the_same_model_before_fallback()
    {
        var caller = new RetryTestModelCaller(failuresBeforeSuccess: 2);
        var (provider, workflow, primary, _) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal(["Primary", "Primary", "Primary"], caller.ModelNames);
        var metrics = GetMetrics(provider);
        Assert.Equal(3, metrics.Count);
        Assert.All(metrics, metric => Assert.Equal(primary.ModelId, metric.ModelId));
        Assert.All(metrics, metric => Assert.Equal(1, metric.AttemptNumber));
        Assert.Equal([1, 2, 3], metrics.Select(metric => metric.TryNumber));
        Assert.Equal([false, false, true], metrics.Select(metric => metric.Succeeded));
        Assert.Single(metrics.Select(metric => metric.LogicalCallId).Distinct());
    }

    [Fact]
    public async Task Exhausted_retries_advance_to_the_fallback_model()
    {
        var caller = new RetryTestModelCaller(failuresBeforeSuccess: int.MaxValue);
        var (provider, workflow, primary, fallback) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal(["Primary", "Primary", "Primary", "Fallback"], caller.ModelNames);
        var metrics = GetMetrics(provider);
        Assert.Equal(4, metrics.Count);
        Assert.Equal([1, 1, 1, 2], metrics.Select(metric => metric.AttemptNumber));
        Assert.Equal([1, 2, 3, 1], metrics.Select(metric => metric.TryNumber));
        Assert.Equal([primary.ModelId, primary.ModelId, primary.ModelId, fallback.ModelId], metrics.Select(metric => metric.ModelId));
        Assert.True(metrics[^1].Succeeded);
    }

    [Fact]
    public async Task Non_transient_failure_is_not_retried()
    {
        var failure = new HttpRequestException("Bad request", null, System.Net.HttpStatusCode.BadRequest);
        var caller = new RetryTestModelCaller(failuresBeforeSuccess: int.MaxValue, failureException: failure);
        var (provider, workflow, _, _) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal(["Primary"], caller.ModelNames);
        var metric = Assert.Single(GetMetrics(provider));
        Assert.Equal(1, metric.TryNumber);
        Assert.Equal(ModelFallbackFailureCategory.InvalidRequest, metric.FailureCategory);
    }

    [Fact]
    public async Task Retry_is_refused_after_response_output_has_started()
    {
        var caller = new RetryTestModelCaller(failuresBeforeSuccess: int.MaxValue, startResponseBeforeFailure: true);
        var (provider, workflow, _, _) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal(["Primary"], caller.ModelNames);
        Assert.Empty(caller.RetryContexts);
        var metric = Assert.Single(GetMetrics(provider));
        Assert.Equal(1, metric.TryNumber);
    }

    [Fact]
    public async Task Provider_override_can_refuse_a_retry_the_default_policy_allows()
    {
        var caller = new RetryTestModelCaller(failuresBeforeSuccess: int.MaxValue, retryOverride: new ModelRetryDecision(false, TimeSpan.Zero));
        var (provider, workflow, primary, fallback) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal(["Primary", "Fallback"], caller.ModelNames);
        var context = caller.RetryContexts[0];
        Assert.True(context.DefaultDecision.ShouldRetry);
        Assert.Equal(1, context.TryNumber);
        Assert.Equal(3, context.MaxTries);
        Assert.Equal(primary.ModelId, context.FailedModel.ModelId);
        Assert.Equal(2, context.ConfiguredModelCount);
        Assert.Equal([primary.ModelId, fallback.ModelId], GetMetrics(provider).Select(metric => metric.ModelId));
    }

    [Fact]
    public async Task Host_override_wins_over_the_provider_override()
    {
        var caller = new RetryTestModelCaller(failuresBeforeSuccess: 1, retryOverride: new ModelRetryDecision(false, TimeSpan.Zero));
        var notification = new RetryOverrideNotification(new ModelRetryDecision(true, TimeSpan.Zero));
        var (provider, workflow, _, _) = await CreateProviderAndWorkflow(caller, notification);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal(["Primary", "Primary"], caller.ModelNames);
        Assert.Single(notification.Contexts);
    }

    [Fact]
    public async Task Host_override_cannot_exceed_the_configured_try_budget()
    {
        var caller = new RetryTestModelCaller(failuresBeforeSuccess: int.MaxValue, failingModelNames: new HashSet<string>(StringComparer.Ordinal) { "Primary", "Fallback" });
        var notification = new RetryOverrideNotification(new ModelRetryDecision(true, TimeSpan.Zero));
        var (provider, workflow, _, _) = await CreateProviderAndWorkflow(caller, notification);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal(["Primary", "Primary", "Primary", "Fallback", "Fallback", "Fallback"], caller.ModelNames);
        Assert.Equal([1, 2, 3, 1, 2, 3], GetMetrics(provider).Select(metric => metric.TryNumber));
    }

    [Fact]
    public async Task Retries_can_be_disabled_by_configuration()
    {
        var caller = new RetryTestModelCaller(failuresBeforeSuccess: int.MaxValue);
        var (provider, workflow, _, _) = await CreateProviderAndWorkflow(caller, maxTries: 1);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal(["Primary", "Fallback"], caller.ModelNames);
        Assert.Empty(caller.RetryContexts);
    }

    private static List<ModelCallMetric> GetMetrics(ServiceProvider provider)
    {
        var repository = Assert.IsType<TestRepositoryService>(provider.GetRequiredService<IRepositoryService>());
        return repository.GetModelCallMetrics().OrderBy(metric => metric.AttemptNumber).ThenBy(metric => metric.TryNumber).ToList();
    }

    private static async Task<(ServiceProvider Provider, WorkflowEntity Workflow, Model Primary, Model Fallback)> CreateProviderAndWorkflow(
        RetryTestModelCaller caller,
        IEngineNotification? notification = null,
        int maxTries = 3
    )
    {
        var provider = WorkflowRunner.BuildProvider(services =>
        {
            // Keep the policy under test but remove the wall-clock cost of backing off.
            services.Configure<ModelRetryOptions>(options =>
            {
                options.MaxTries = maxTries;
                options.BaseDelay = TimeSpan.Zero;
            });
            services.AddKeyedSingleton<IModelCaller>("retry-test", caller);
            if (notification is not null)
                services.AddSingleton(notification);
        });

        var repository = provider.GetRequiredService<IRepositoryService>();
        var primary = CreateModel("Primary");
        var fallback = CreateModel("Fallback");
        await SeedMetadata(repository, primary);
        await SeedMetadata(repository, fallback);

        var workflow = new WorkflowBuilder().WithName("Retry Workflow").AddStart().AddModelCall("model").AddEnd().Connect("start", "model").Connect("model", "end").Build();
        WorkflowSchemaUpgrader.Upgrade(workflow);
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Prompt = "Say hello";
        node.Models =
        [
            new ModelCallModelDefinition { ModelId = primary.ModelId, ParameterValues = [] },
            new ModelCallModelDefinition { ModelId = fallback.ModelId, ParameterValues = [] },
        ];
        await repository.UpsertWorkflow(workflow);
        return (provider, workflow, primary, fallback);
    }

    private static async Task<Run> RunWorkflow(ServiceProvider provider, WorkflowEntity workflow)
    {
        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engine.StartWorkflowRunAndWait(workflow.Id, new ContextObject());
            await Task.Delay(50);
            return run;
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    private static Model CreateModel(string name)
    {
        return new Model
        {
            Version = 1,
            ModelId = Guid.NewGuid(),
            Name = name,
            Description = name,
            ConfigId = "retry-model-config",
            ConnectorId = Guid.NewGuid(),
            CustomCapabilities = [],
            ParameterValues = [],
        };
    }

    private static async Task SeedMetadata(IRepositoryService repository, Model model)
    {
        await repository.UpsertConnectorConfig(
            new ConnectorConfig
            {
                Version = 1,
                ConfigId = "retry-test",
                DisplayName = "Retry test",
                Description = string.Empty,
                AuthModes = [],
            }
        );
        await repository.UpsertConnector(
            new Connector
            {
                Version = 1,
                ConnectorId = model.ConnectorId!.Value,
                Name = $"{model.Name} connector",
                Description = string.Empty,
                ConfigId = "retry-test",
                AuthenticationModeId = string.Empty,
                FieldValues = [],
            }
        );
        await repository.UpsertModelConfig(
            new ModelConfig
            {
                Version = 1,
                ConfigId = model.ConfigId,
                DisplayName = "Retry model",
                Description = string.Empty,
                ConnectorConfigId = "retry-test",
                IsCustom = false,
                Capabilities = [new ModelCapability { Name = "SupportsTextIn", DisplayName = "Text input" }],
                ParameterFields = [],
            }
        );
        await repository.UpsertModel(model);
    }

    private sealed class RetryTestModelCaller(
        int failuresBeforeSuccess = 2,
        Exception? failureException = null,
        bool startResponseBeforeFailure = false,
        ModelRetryDecision? retryOverride = null,
        IReadOnlySet<string>? failingModelNames = null
    ) : IModelCaller
    {
        private readonly HashSet<string> _failingModelNames =
            failingModelNames is null ? new HashSet<string>(StringComparer.Ordinal) { "Primary" } : new HashSet<string>(failingModelNames, StringComparer.Ordinal);
        private readonly Dictionary<string, int> _failuresByModel = new(StringComparer.Ordinal);

        public List<string> ModelNames { get; } = [];
        public List<ModelRetryDecisionContext> RetryContexts { get; } = [];

        public ModelRetryDecision? ModelRetryOverride(ModelRetryDecisionContext context)
        {
            RetryContexts.Add(context);
            return retryOverride;
        }

        public async Task<ModelCallResult> Call(
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
            var name = model.Name ?? string.Empty;
            ModelNames.Add(name);

            if (_failingModelNames.Contains(name) && _failuresByModel.GetValueOrDefault(name) < failuresBeforeSuccess)
            {
                _failuresByModel[name] = _failuresByModel.GetValueOrDefault(name) + 1;

                if (startResponseBeforeFailure)
                    await progressSink.OnTextDeltaAsync("partial", "partial");

                throw failureException ?? new HttpRequestException("Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);
            }

            return new ModelCallResult
            {
                Chat = [],
                Responses = [new ChatMessage(ChatRole.Assistant, [new TextContent("ok")])],
                ResultValue = "ok",
                ProviderModelName = "retry-provider-model",
            };
        }
    }

    private sealed class RetryOverrideNotification(ModelRetryDecision? decision) : IEngineNotification
    {
        public List<ModelRetryDecisionContext> Contexts { get; } = [];

        public ValueTask<ModelRetryDecision?> ModelRetryOverride(ModelRetryDecisionContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return ValueTask.FromResult(decision);
        }
    }
}
