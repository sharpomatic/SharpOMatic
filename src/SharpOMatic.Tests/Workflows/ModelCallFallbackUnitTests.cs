namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallFallbackUnitTests
{
    [Theory]
    [InlineData(403, ModelFallbackFailureCategory.Authentication, false)]
    [InlineData(408, ModelFallbackFailureCategory.Timeout, true)]
    [InlineData(429, ModelFallbackFailureCategory.RateLimited, true)]
    [InlineData(503, ModelFallbackFailureCategory.ProviderUnavailable, true)]
    public void Standard_classifier_translates_http_status_codes(int statusCode, ModelFallbackFailureCategory expectedCategory, bool expectedTransient)
    {
        var exception = new HttpRequestException("Provider error", null, (System.Net.HttpStatusCode)statusCode);

        var failure = ModelFallbackFailureClassifier.Classify(exception);

        Assert.Equal(expectedCategory, failure.Category);
        Assert.Equal(statusCode, failure.StatusCode);
        Assert.Equal(expectedTransient, failure.IsTransient);
    }

    [Fact]
    public async Task Transient_primary_failure_uses_fallback_model_and_its_parameters()
    {
        var caller = new FallbackTestModelCaller();
        var (provider, workflow, primary, fallback) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal([primary.ModelId, fallback.ModelId], caller.ModelIds);
        Assert.Equal(["primary", "fallback"], caller.ParameterValues);
        Assert.True(run.OutputContext?.Contains("fallback", StringComparison.Ordinal));
        var repository = Assert.IsType<TestRepositoryService>(provider.GetRequiredService<IRepositoryService>());
        var metrics = repository.GetModelCallMetrics().OrderBy(metric => metric.AttemptNumber).ToList();
        Assert.Equal(2, metrics.Count);
        Assert.NotEqual(Guid.Empty, metrics[0].LogicalCallId);
        Assert.Equal(metrics[0].LogicalCallId, metrics[1].LogicalCallId);
        Assert.Equal(1, metrics[0].AttemptNumber);
        Assert.Equal(primary.ModelId, metrics[0].ModelId);
        Assert.False(metrics[0].Succeeded);
        Assert.Equal(ModelFallbackFailureCategory.ProviderUnavailable, metrics[0].FailureCategory);
        Assert.Equal(503, metrics[0].ProviderStatusCode);
        Assert.Equal(2, metrics[1].AttemptNumber);
        Assert.Equal(fallback.ModelId, metrics[1].ModelId);
        Assert.True(metrics[1].Succeeded);
        Assert.Null(metrics[1].FailureCategory);
        Assert.Null(metrics[1].ProviderStatusCode);
        var workflowMetric = Assert.Single(repository.GetWorkflowRunMetrics());
        Assert.True(workflowMetric.Succeeded);
        Assert.Equal(2, workflowMetric.ModelCallCount);
        Assert.Equal(1, workflowMetric.ModelCallFailureCount);
    }

    [Fact]
    public async Task Disabled_primary_is_skipped_and_fallback_is_attempt_one()
    {
        var caller = new FallbackTestModelCaller();
        var (provider, workflow, _, fallback) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Models[0].Disabled = true;
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal([fallback.ModelId], caller.ModelIds);
        Assert.Equal(["fallback"], caller.ParameterValues);
        var metric = Assert.Single(Assert.IsType<TestRepositoryService>(repository).GetModelCallMetrics());
        Assert.Equal(1, metric.AttemptNumber);
        Assert.Equal(fallback.ModelId, metric.ModelId);
        Assert.True(metric.Succeeded);
    }

    [Fact]
    public async Task Disabled_fallback_is_not_attempted()
    {
        var caller = new FallbackTestModelCaller();
        var (provider, workflow, primary, _) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Models[1].Disabled = true;
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal([primary.ModelId], caller.ModelIds);
        var metric = Assert.Single(Assert.IsType<TestRepositoryService>(repository).GetModelCallMetrics());
        Assert.Equal(1, metric.AttemptNumber);
        Assert.Equal(primary.ModelId, metric.ModelId);
        Assert.False(metric.Succeeded);
    }

    [Fact]
    public async Task All_disabled_models_fail_with_clear_error_without_calling_provider()
    {
        var caller = new FallbackTestModelCaller();
        var (provider, workflow, _, _) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Models.ForEach(model => model.Disabled = true);
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Empty(caller.ModelIds);
        var metric = Assert.Single(Assert.IsType<TestRepositoryService>(repository).GetModelCallMetrics());
        Assert.Equal("No enabled models are configured", metric.ErrorMessage);
        Assert.Equal(typeof(SharpOMaticException).FullName, metric.ErrorType);
    }

    [Fact]
    public async Task Second_fallback_succeeds_after_two_transient_failures()
    {
        var caller = new FallbackTestModelCaller(failingModelNames: new HashSet<string>(StringComparer.Ordinal) { "Primary", "Fallback" });
        var (provider, workflow, primary, fallback) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        var finalFallback = CreateModel("Second fallback");
        await SeedMetadata(repository, finalFallback);
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Models.Add(new ModelCallModelDefinition { ModelId = finalFallback.ModelId, ParameterValues = new Dictionary<string, string?> { ["attempt_value"] = "second-fallback" } });
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal([primary.ModelId, fallback.ModelId, finalFallback.ModelId], caller.ModelIds);
        var metrics = Assert.IsType<TestRepositoryService>(repository).GetModelCallMetrics().OrderBy(metric => metric.AttemptNumber).ToList();
        Assert.Equal([1, 2, 3], metrics.Select(metric => metric.AttemptNumber));
        Assert.Equal([false, false, true], metrics.Select(metric => metric.Succeeded));
        Assert.Single(metrics.Select(metric => metric.LogicalCallId).Distinct());
    }

    [Fact]
    public async Task Disabled_middle_fallback_is_skipped_in_attempt_numbering()
    {
        var caller = new FallbackTestModelCaller();
        var (provider, workflow, primary, _) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        var finalFallback = CreateModel("Second fallback");
        await SeedMetadata(repository, finalFallback);
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Models[1].Disabled = true;
        node.Models.Add(new ModelCallModelDefinition { ModelId = finalFallback.ModelId, ParameterValues = new Dictionary<string, string?> { ["attempt_value"] = "second-fallback" } });
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal([primary.ModelId, finalFallback.ModelId], caller.ModelIds);
        var metrics = Assert.IsType<TestRepositoryService>(repository).GetModelCallMetrics().OrderBy(metric => metric.AttemptNumber).ToList();
        Assert.Equal([1, 2], metrics.Select(metric => metric.AttemptNumber));
        Assert.Equal([primary.ModelId, finalFallback.ModelId], metrics.Select(metric => metric.ModelId));
    }

    [Fact]
    public async Task Three_terminal_failures_preserve_all_attempt_metrics()
    {
        var failingNames = new HashSet<string>(StringComparer.Ordinal) { "Primary", "Fallback", "Final fallback" };
        var caller = new FallbackTestModelCaller(failingModelNames: failingNames);
        var (provider, workflow, primary, fallback) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        var finalFallback = CreateModel("Final fallback");
        await SeedMetadata(repository, finalFallback);
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Models.Add(new ModelCallModelDefinition { ModelId = finalFallback.ModelId, ParameterValues = new Dictionary<string, string?> { ["attempt_value"] = "final-fallback" } });
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal([primary.ModelId, fallback.ModelId, finalFallback.ModelId], caller.ModelIds);
        var metrics = Assert.IsType<TestRepositoryService>(repository).GetModelCallMetrics().OrderBy(metric => metric.AttemptNumber).ToList();
        Assert.Equal(3, metrics.Count);
        Assert.All(metrics, metric => Assert.False(metric.Succeeded));
        Assert.All(metrics, metric => Assert.Equal(ModelFallbackFailureCategory.ProviderUnavailable, metric.FailureCategory));
        Assert.Single(metrics.Select(metric => metric.LogicalCallId).Distinct());
    }

    [Fact]
    public async Task Compatible_primary_tool_and_structured_values_are_used_by_fallback()
    {
        var caller = new FallbackTestModelCaller();
        var (provider, workflow, _, _) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        await repository.UpsertModelConfig(CreateCapabilityModelConfig("fallback-model-config", ["None", "Auto", "Required"], ["Text", "Json", "Schema", "Configured Type"]));
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Models[0].ParameterValues["selected_tools"] = "Search,Weather";
        node.Models[0].ParameterValues["tool_choice"] = "Required";
        node.Models[0].ParameterValues["parallel_tool_calls"] = "True";
        node.Models[0].ParameterValues["structured_output"] = "Schema";
        node.Models[0].ParameterValues["structured_output_schema"] = "{\"type\":\"object\"}";
        node.Models[1].ParameterValues["tool_choice"] = "Auto";
        node.Models[1].ParameterValues["parallel_tool_calls"] = "False";
        node.Models[1].ParameterValues["structured_output"] = "Text";
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        var fallbackValues = caller.ParameterSnapshots[1];
        Assert.Equal("Search,Weather", fallbackValues["selected_tools"]);
        Assert.Equal("Required", fallbackValues["tool_choice"]);
        Assert.Equal("True", fallbackValues["parallel_tool_calls"]);
        Assert.Equal("Schema", fallbackValues["structured_output"]);
        Assert.Equal("{\"type\":\"object\"}", fallbackValues["structured_output_schema"]);
    }

    [Fact]
    public async Task Incompatible_primary_values_do_not_replace_fallback_values()
    {
        var caller = new FallbackTestModelCaller();
        var (provider, workflow, primary, fallback) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        primary.ConfigId = "primary-capabilities";
        fallback.ConfigId = "fallback-limited-capabilities";
        await repository.UpsertModelConfig(CreateCapabilityModelConfig(primary.ConfigId, ["None", "Auto", "Required"], ["Text", "Json", "Schema"]));
        await repository.UpsertModelConfig(CreateCapabilityModelConfig(fallback.ConfigId, ["None", "Auto"], ["Text", "Json"]));
        await repository.UpsertModel(primary);
        await repository.UpsertModel(fallback);
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Models[0].ParameterValues["selected_tools"] = "Search";
        node.Models[0].ParameterValues["tool_choice"] = "Required";
        node.Models[0].ParameterValues["structured_output"] = "Schema";
        node.Models[0].ParameterValues["structured_output_schema"] = "{\"type\":\"object\"}";
        node.Models[1].ParameterValues["tool_choice"] = "Auto";
        node.Models[1].ParameterValues["structured_output"] = "Text";
        await repository.UpsertWorkflow(workflow);

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        var fallbackValues = caller.ParameterSnapshots[1];
        Assert.Equal("Search", fallbackValues["selected_tools"]);
        Assert.Equal("Auto", fallbackValues["tool_choice"]);
        Assert.Equal("Text", fallbackValues["structured_output"]);
        Assert.False(fallbackValues.ContainsKey("structured_output_schema"));
    }

    [Fact]
    public async Task Model_fallback_override_can_veto_default_transient_fallback()
    {
        var caller = new FallbackTestModelCaller();
        var notification = new FallbackOverrideNotification(false);
        var (provider, workflow, primary, _) = await CreateProviderAndWorkflow(caller, notification);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal([primary.ModelId], caller.ModelIds);
        Assert.Single(notification.Contexts);
        Assert.True(notification.Contexts[0].DefaultDecision);
        Assert.Equal(ModelFallbackFailureCategory.ProviderUnavailable, notification.Contexts[0].Failure.Category);
    }

    [Fact]
    public async Task Model_fallback_override_can_allow_safe_non_transient_failure()
    {
        var caller = new FallbackTestModelCaller(useTransientFailure: false);
        var notification = new FallbackOverrideNotification(true);
        var (provider, workflow, primary, fallback) = await CreateProviderAndWorkflow(caller, notification);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        Assert.Equal([primary.ModelId, fallback.ModelId], caller.ModelIds);
        Assert.False(notification.Contexts[0].DefaultDecision);
    }

    [Fact]
    public async Task Provider_failure_override_classifies_custom_error_before_standard_translation()
    {
        var providerFailure = new ModelFallbackFailure(ModelFallbackFailureCategory.ProviderUnavailable, 529, RetryAfter: null, IsTransient: true);
        var caller = new FallbackTestModelCaller(useTransientFailure: false, providerFailureOverride: providerFailure);
        var (provider, workflow, _, _) = await CreateProviderAndWorkflow(caller);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Success, run.RunStatus);
        var metrics = Assert.IsType<TestRepositoryService>(provider.GetRequiredService<IRepositoryService>()).GetModelCallMetrics().OrderBy(metric => metric.AttemptNumber).ToList();
        Assert.Equal(2, metrics.Count);
        Assert.Equal(ModelFallbackFailureCategory.ProviderUnavailable, metrics[0].FailureCategory);
        Assert.Equal(529, metrics[0].ProviderStatusCode);
    }

    [Fact]
    public async Task Override_cannot_force_fallback_after_response_output_starts()
    {
        var caller = new FallbackTestModelCaller(startResponseBeforeFailure: true);
        var notification = new FallbackOverrideNotification(true);
        var (provider, workflow, primary, _) = await CreateProviderAndWorkflow(caller, notification);
        await using var providerScope = provider;

        var run = await RunWorkflow(provider, workflow);

        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal([primary.ModelId], caller.ModelIds);
        Assert.Empty(notification.Contexts);
    }

    private static async Task<(ServiceProvider Provider, WorkflowEntity Workflow, Model Primary, Model Fallback)> CreateProviderAndWorkflow(
        FallbackTestModelCaller caller,
        IEngineNotification? notification = null
    )
    {
        var provider = WorkflowRunner.BuildProvider(services =>
        {
            services.AddKeyedSingleton<IModelCaller>("fallback-test", caller);
            if (notification is not null)
                services.AddSingleton(notification);
        });
        var repository = provider.GetRequiredService<IRepositoryService>();
        var primary = CreateModel("Primary");
        var fallback = CreateModel("Fallback");
        await SeedMetadata(repository, primary);
        await SeedMetadata(repository, fallback);

        var workflow = new WorkflowBuilder().WithName("Fallback Workflow").AddStart().AddModelCall("model").AddEnd().Connect("start", "model").Connect("model", "end").Build();
        WorkflowSchemaUpgrader.Upgrade(workflow);
        var node = Assert.IsType<ModelCallNodeEntity>(workflow.Nodes.Single(item => item.Title == "model"));
        node.Prompt = "Say hello";
        node.Models =
        [
            new ModelCallModelDefinition { ModelId = primary.ModelId, ParameterValues = new Dictionary<string, string?> { ["attempt_value"] = "primary" } },
            new ModelCallModelDefinition { ModelId = fallback.ModelId, ParameterValues = new Dictionary<string, string?> { ["attempt_value"] = "fallback" } },
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
            ConfigId = "fallback-model-config",
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
                ConfigId = "fallback-test",
                DisplayName = "Fallback test",
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
                ConfigId = "fallback-test",
                AuthenticationModeId = string.Empty,
                FieldValues = [],
            }
        );
        await repository.UpsertModelConfig(
            new ModelConfig
            {
                Version = 1,
                ConfigId = model.ConfigId,
                DisplayName = "Fallback model",
                Description = string.Empty,
                ConnectorConfigId = "fallback-test",
                IsCustom = false,
                Capabilities = [new ModelCapability { Name = "SupportsTextIn", DisplayName = "Text input" }],
                ParameterFields = [],
            }
        );
        await repository.UpsertModel(model);
    }

    private static ModelConfig CreateCapabilityModelConfig(string configId, List<string> toolChoices, List<string> structuredOutputTypes)
    {
        return new ModelConfig
        {
            Version = 1,
            ConfigId = configId,
            DisplayName = configId,
            Description = string.Empty,
            ConnectorConfigId = "fallback-test",
            IsCustom = false,
            Capabilities =
            [
                new ModelCapability { Name = "SupportsTextIn", DisplayName = "Text input" },
                new ModelCapability { Name = "SupportsToolCalling", DisplayName = "Tool calling" },
                new ModelCapability { Name = "SupportsStructuredOutput", DisplayName = "Structured output" },
            ],
            ParameterFields =
            [
                new FieldDescriptor { Name = "tool_choice", Label = "Tool Choice", Description = string.Empty, CallDefined = true, Type = FieldDescriptorType.Enum, IsRequired = false, Capability = "SupportsToolCalling", DefaultValue = "Auto", EnumOptions = toolChoices },
                new FieldDescriptor { Name = "parallel_tool_calls", Label = "Parallel Tool Calls", Description = string.Empty, CallDefined = true, Type = FieldDescriptorType.Boolean, IsRequired = false, Capability = "SupportsToolCalling", DefaultValue = true },
                new FieldDescriptor { Name = "structured_output", Label = "Structured Output", Description = string.Empty, CallDefined = true, Type = FieldDescriptorType.Enum, IsRequired = false, Capability = "SupportsStructuredOutput", DefaultValue = "Text", EnumOptions = structuredOutputTypes },
            ],
        };
    }

    private sealed class FallbackTestModelCaller(
        bool useTransientFailure = true,
        bool startResponseBeforeFailure = false,
        ModelFallbackFailure? providerFailureOverride = null,
        IReadOnlySet<string>? failingModelNames = null
    ) : IModelCaller
    {
        private readonly HashSet<string> _failingModelNames = failingModelNames is null ? new HashSet<string>(StringComparer.Ordinal) { "Primary" } : new HashSet<string>(failingModelNames, StringComparer.Ordinal);
        public List<Guid> ModelIds { get; } = [];
        public List<string?> ParameterValues { get; } = [];
        public List<Dictionary<string, string?>> ParameterSnapshots { get; } = [];

        public ModelFallbackFailure? ModelFallbackFailureOverride(Exception exception) => providerFailureOverride;

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
            ModelIds.Add(model.ModelId);
            ParameterValues.Add((node.ParameterValues ?? []).GetValueOrDefault("attempt_value"));
            ParameterSnapshots.Add(new Dictionary<string, string?>(node.ParameterValues ?? [], StringComparer.Ordinal));
            if (_failingModelNames.Contains(model.Name))
            {
                if (startResponseBeforeFailure)
                    await progressSink.OnTextDeltaAsync("partial", "partial");

                if (useTransientFailure)
                    throw new HttpRequestException("Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);

                throw new InvalidOperationException("Custom host failure");
            }

            return new ModelCallResult
            {
                Chat = [],
                Responses = [new ChatMessage(ChatRole.Assistant, [new TextContent("fallback")])],
                ResultValue = "fallback",
                ProviderModelName = "fallback-provider-model",
            };
        }
    }

    private sealed class FallbackOverrideNotification(bool decision) : IEngineNotification
    {
        public List<ModelFallbackDecisionContext> Contexts { get; } = [];

        public ValueTask<bool?> ModelFallbackOverride(ModelFallbackDecisionContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return ValueTask.FromResult<bool?>(decision);
        }
    }
}
