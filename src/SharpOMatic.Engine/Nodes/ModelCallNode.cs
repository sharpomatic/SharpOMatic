#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.ModelCall)]
public class ModelCallNode(ThreadContext threadContext, ModelCallNodeEntity node) : RunNode<ModelCallNodeEntity>(threadContext, node)
{
    protected override async Task<NodeExecutionResult> RunInternal()
    {
        var logicalCallId = Guid.NewGuid();
        var currentStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var metric = CreateMetric(logicalCallId, attemptNumber: 1);
        var metricWritten = false;
        IModelCaller? currentCaller = null;

        try
        {
            if (Node.Models.Count == 0)
                throw new SharpOMaticException("No model selected");

            var enabledDefinitions = Node.Models.Where(definition => !definition.Disabled).ToList();
            if (enabledDefinitions.Count == 0)
                throw new SharpOMaticException("No enabled models are configured");

            metric.ModelId = enabledDefinitions[0].ModelId;
            var attempts = new List<ModelCallAttemptResources>(enabledDefinitions.Count);
            foreach (var definition in enabledDefinitions)
                attempts.Add(await LoadModelAndConnector(definition));

            ValidateFallbackCapabilities(attempts);

            for (var attemptIndex = 0; attemptIndex < attempts.Count; attemptIndex += 1)
            {
                var attempt = attempts[attemptIndex];
                metric = CreateMetric(logicalCallId, attemptIndex + 1);
                metricWritten = false;
                currentCaller = attempt.Caller;
                ApplyMetricIdentity(metric, attempt);
                ApplyActivityIdentity(attempt, attemptIndex);

                var attemptNode = CreateAttemptNode(attempt, disablePromptStream: attemptIndex > 0);
                var progressSink = new ModelCallNodeProgressSink(ProcessContext, Trace, Informations, attemptNode);
                currentStopwatch = System.Diagnostics.Stopwatch.StartNew();
                ModelCallResult result;
                try
                {
                    result = await attempt.Caller.Call(
                        attempt.Model,
                        attempt.ModelConfig,
                        attempt.Connector,
                        attempt.ConnectorConfig,
                        ProcessContext,
                        ThreadContext,
                        attemptNode,
                        progressSink
                    );
                }
                catch (Exception exception)
                {
                    currentStopwatch.Stop();
                    var failure = ClassifyFailure(attempt.Caller, exception);
                    ApplyFailure(metric, exception, failure, currentStopwatch.ElapsedMilliseconds);
                    await AppendFailureMetric(metric);
                    metricWritten = true;

                    if (attemptIndex + 1 >= attempts.Count || !await ShouldUseFallback(exception, failure, progressSink, attempts, attemptIndex))
                        throw;

                    continue;
                }

                ApplyUsage(metric, result, attempt.ModelConfig);

                if (NodeActivity is not null)
                {
                    if (metric.ProviderModelName is not null)
                        NodeActivity.SetTag("gen_ai.request.model", metric.ProviderModelName);
                    if (metric.InputTokens.HasValue)
                        NodeActivity.SetTag("gen_ai.usage.input_tokens", metric.InputTokens.Value);
                    if (metric.OutputTokens.HasValue)
                        NodeActivity.SetTag("gen_ai.usage.output_tokens", metric.OutputTokens.Value);
                    if (metric.TotalCost.HasValue)
                        NodeActivity.SetTag("sharpomatic.model_call.total_cost", (double)metric.TotalCost.Value);
                }

                if (!string.IsNullOrWhiteSpace(Node.TextOutputPath) && !ThreadContext.NodeContext.TrySet(Node.TextOutputPath, result.ResultValue))
                    throw new SharpOMaticException($"Could not set '{Node.TextOutputPath}' into context.");

                if (Node.BatchOutput)
                    WriteChatOutput(result.Chat, result.Responses);

                await progressSink.ApplyBatchResponseFallbackAsync(result.Responses);
                await progressSink.CompleteAsync();

                if (!Node.BatchOutput)
                    WriteChatOutput(result.Chat, result.Responses);

                ApplyExitContext(result);

                await progressSink.PersistAsync();

                currentStopwatch.Stop();
                metric.Duration = currentStopwatch.ElapsedMilliseconds;
                metric.Succeeded = true;
                await ProcessContext.RepositoryService.AppendModelCallMetric(metric);
                metricWritten = true;

                return NodeExecutionResult.Continue($"{attempt.Model.Name ?? "(empty)"}", ResolveOptionalSingleOutput(ThreadContext));
            }

            throw new SharpOMaticException("No model call attempt completed.");
        }
        catch (Exception ex)
        {
            if (!metricWritten)
            {
                currentStopwatch.Stop();
                ApplyFailure(metric, ex, ClassifyFailure(currentCaller, ex), currentStopwatch.ElapsedMilliseconds);
                await AppendFailureMetric(metric);
            }

            throw;
        }
    }

    private ModelCallMetric CreateMetric(Guid logicalCallId, int attemptNumber)
    {
        return new ModelCallMetric()
        {
            Id = Guid.NewGuid(),
            LogicalCallId = logicalCallId,
            AttemptNumber = attemptNumber,
            Created = DateTime.UtcNow,
            Succeeded = false,
            WorkflowId = WorkflowContext.Workflow.Id,
            WorkflowName = WorkflowContext.Workflow.Name,
            RunId = ProcessContext.Run.RunId,
            ConversationId = ProcessContext.Run.ConversationId,
            NodeEntityId = Node.Id,
            NodeTitle = Node.Title,
        };
    }

    private static void ApplyFailure(ModelCallMetric metric, Exception exception, ModelFallbackFailure failure, long duration)
    {
        metric.Duration = duration;
        metric.Succeeded = false;
        metric.ErrorMessage = exception.Message;
        metric.ErrorType = exception.GetType().FullName;
        metric.FailureCategory = failure.Category;
        metric.ProviderStatusCode = failure.StatusCode;
    }

    private static ModelFallbackFailure ClassifyFailure(IModelCaller? caller, Exception exception)
    {
        if (caller is not null)
        {
            try
            {
                var providerFailure = caller.ModelFallbackFailureOverride(exception);
                if (providerFailure is not null)
                    return providerFailure;
            }
            catch
            {
            }
        }

        return ModelFallbackFailureClassifier.Classify(exception);
    }

    private static void ApplyUsage(ModelCallMetric metric, ModelCallResult result, ModelConfig modelConfig)
    {
        metric.ProviderModelName = result.ProviderModelName;

        if (result.Usage is null)
            return;

        metric.InputTokens = result.Usage.InputTokenCount;
        metric.OutputTokens = result.Usage.OutputTokenCount;
        metric.TotalTokens = result.Usage.TotalTokenCount;

        var inputPrice = GetInformationDecimal(modelConfig, "InputPrice");
        var outputPrice = GetInformationDecimal(modelConfig, "OutputPrice");

        if (metric.InputTokens.HasValue && inputPrice.HasValue)
            metric.InputCost = CalculateCost(metric.InputTokens.Value, inputPrice.Value);

        if (metric.OutputTokens.HasValue && outputPrice.HasValue)
            metric.OutputCost = CalculateCost(metric.OutputTokens.Value, outputPrice.Value);

        if (metric.InputCost.HasValue || metric.OutputCost.HasValue)
            metric.TotalCost = (metric.InputCost ?? 0) + (metric.OutputCost ?? 0);
    }

    private static decimal? GetInformationDecimal(ModelConfig modelConfig, string name)
    {
        var value = modelConfig.Information?.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal))?.Value;
        return value switch
        {
            null => null,
            decimal decimalValue => decimalValue,
            double doubleValue => (decimal)doubleValue,
            float floatValue => (decimal)floatValue,
            int intValue => intValue,
            long longValue => longValue,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonElement element when element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), out var decimalValue) => decimalValue,
            string stringValue when decimal.TryParse(stringValue, out var decimalValue) => decimalValue,
            _ => null,
        };
    }

    private static decimal CalculateCost(long tokens, decimal pricePerMillionTokens)
    {
        return tokens / 1_000_000m * pricePerMillionTokens;
    }

    private async Task AppendFailureMetric(ModelCallMetric metric)
    {
        try
        {
            await ProcessContext.RepositoryService.AppendModelCallMetric(metric);
        }
        catch
        {
        }
    }

    private void WriteChatOutput(IList<ChatMessage> chat, IList<ChatMessage> responses)
    {
        if (!string.IsNullOrWhiteSpace(Node.ChatOutputPath))
        {
            ThreadContext.NodeContext.TrySet(Node.ChatOutputPath, ChatHistoryReplayHelper.CreatePortableOutputMessages(chat.Concat(responses), Node.DropToolCalls));
        }
    }

    private void ApplyExitContext(ModelCallResult result)
    {
        if (result.ExitContext is null)
            return;

        var path = string.IsNullOrWhiteSpace(result.ExitContextPath)
            ? "exit"
            : result.ExitContextPath.Trim();

        if (!ThreadContext.NodeContext.TrySet(path, result.ExitContext))
            throw new SharpOMaticException($"Could not set model call exit context at '{path}'.");
    }

    private async Task<ModelCallAttemptResources> LoadModelAndConnector(ModelCallModelDefinition definition)
    {
        var model = await ProcessContext.RepositoryService.GetModel(definition.ModelId);
        if (model is null)
            throw new SharpOMaticException($"Cannot find model '{definition.ModelId}'");

        if (model.ConnectorId is null)
            throw new SharpOMaticException("Model has no connector defined");

        var modelConfig = await ProcessContext.RepositoryService.GetModelConfig(model.ConfigId);
        if (modelConfig is null)
            throw new SharpOMaticException("Cannot find the model configuration");

        var connector = await ProcessContext.RepositoryService.GetConnector(model.ConnectorId.Value, false);
        if (connector is null)
            throw new SharpOMaticException("Cannot find the model connector");

        var connectorConfig = await ProcessContext.RepositoryService.GetConnectorConfig(connector.ConfigId);
        if (connectorConfig is null)
            throw new SharpOMaticException("Cannot find the connector configuration");

        var caller = ProcessContext.ServiceScope.ServiceProvider.GetKeyedService<IModelCaller>(connectorConfig.ConfigId);
        if (caller is null)
            throw new SharpOMaticException($"No implementation found for connector '{connectorConfig.ConfigId}'");

        return new ModelCallAttemptResources(definition, model, modelConfig, connector, connectorConfig, caller);
    }

    private void ValidateFallbackCapabilities(IReadOnlyList<ModelCallAttemptResources> attempts)
    {
        var requiredCapabilities = new HashSet<string>(StringComparer.Ordinal) { "SupportsTextIn" };
        var primary = Node.Models[0];

        if (!string.IsNullOrWhiteSpace(Node.ImageInputPath))
            requiredCapabilities.Add("SupportsImageIn");

        if (primary.ParameterValues.TryGetValue("selected_tools", out var selectedTools) && !string.IsNullOrWhiteSpace(selectedTools))
            requiredCapabilities.Add("SupportsToolCalling");

        if (
            primary.ParameterValues.TryGetValue("structured_output", out var structuredOutput)
            && !string.IsNullOrWhiteSpace(structuredOutput)
            && !string.Equals(structuredOutput, "Text", StringComparison.Ordinal)
        )
            requiredCapabilities.Add("SupportsStructuredOutput");

        foreach (var attempt in attempts.Where(attempt => !ReferenceEquals(attempt.Definition, primary)))
        {
            foreach (var capability in requiredCapabilities)
            {
                if (!HasCapability(attempt.Model, attempt.ModelConfig, capability))
                    throw new SharpOMaticException($"Fallback model '{attempt.Model.Name}' does not support required capability '{capability}'.");
            }
        }
    }

    private static bool HasCapability(Model model, ModelConfig modelConfig, string capability)
    {
        return modelConfig.Capabilities.Any(item => item.Name == capability)
            && (!modelConfig.IsCustom || model.CustomCapabilities.Contains(capability, StringComparer.Ordinal));
    }

    private async Task<bool> ShouldUseFallback(
        Exception exception,
        ModelFallbackFailure failure,
        ModelCallNodeProgressSink progressSink,
        IReadOnlyList<ModelCallAttemptResources> attempts,
        int failedAttemptIndex
    )
    {
        if (progressSink.ResponseStarted || progressSink.ToolInvocationStarted)
            return false;

        var defaultDecision = failure.IsTransient;
        var context = new ModelFallbackDecisionContext(
            ProcessContext.Run.RunId,
            WorkflowContext.Workflow.Id,
            ProcessContext.Run.ConversationId,
            Node.Id,
            Node.Title,
            failedAttemptIndex,
            attempts.Count,
            CreateFallbackTarget(attempts[failedAttemptIndex]),
            CreateFallbackTarget(attempts[failedAttemptIndex + 1]),
            exception,
            failure,
            progressSink.ResponseStarted,
            progressSink.ToolInvocationStarted,
            defaultDecision,
            defaultDecision ? $"Transient failure category '{failure.Category}'." : $"Failure category '{failure.Category}' is not transient."
        );

        foreach (var notification in ProcessContext.ServiceScope.ServiceProvider.GetServices<IEngineNotification>())
        {
            bool? decision;
            try
            {
                decision = await notification.ModelFallbackOverride(context);
            }
            catch (Exception overrideException)
            {
                throw new SharpOMaticException("Model fallback override failed.", new AggregateException(exception, overrideException));
            }

            if (decision.HasValue)
                return decision.Value;
        }

        return defaultDecision;
    }

    private static ModelFallbackTarget CreateFallbackTarget(ModelCallAttemptResources attempt)
    {
        return new ModelFallbackTarget(
            attempt.Model.ModelId,
            attempt.Model.Name,
            attempt.ModelConfig.ConfigId,
            attempt.Connector.ConnectorId,
            attempt.Connector.Name,
            attempt.ConnectorConfig.ConfigId,
            new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>(attempt.Definition.ParameterValues, StringComparer.Ordinal))
        );
    }

    private void ApplyMetricIdentity(ModelCallMetric metric, ModelCallAttemptResources attempt)
    {
        metric.ModelId = attempt.Model.ModelId;
        metric.ModelName = attempt.Model.Name;
        metric.ModelConfigId = attempt.ModelConfig.ConfigId;
        metric.ModelConfigName = attempt.ModelConfig.DisplayName;
        metric.ConnectorId = attempt.Connector.ConnectorId;
        metric.ConnectorName = attempt.Connector.Name;
        metric.ConnectorConfigId = attempt.ConnectorConfig.ConfigId;
        metric.ConnectorConfigName = attempt.ConnectorConfig.DisplayName;
    }

    private void ApplyActivityIdentity(ModelCallAttemptResources attempt, int attemptIndex)
    {
        NodeActivity?.SetTag("sharpomatic.model.name", attempt.Model.Name);
        NodeActivity?.SetTag("sharpomatic.model.config", attempt.ModelConfig.ConfigId);
        NodeActivity?.SetTag("sharpomatic.connector.name", attempt.Connector.Name);
        NodeActivity?.SetTag("sharpomatic.connector.config", attempt.ConnectorConfig.ConfigId);
        NodeActivity?.SetTag("sharpomatic.model_call.attempt", attemptIndex + 1);
        NodeActivity?.SetTag("sharpomatic.model_call.fallback", attemptIndex > 0);
    }

    private ModelCallNodeEntity CreateAttemptNode(ModelCallAttemptResources attempt, bool disablePromptStream)
    {
        var definition = attempt.Definition;
        var parameterValues = new Dictionary<string, string?>(definition.ParameterValues, StringComparer.Ordinal);
        var primaryParameterValues = Node.Models[0].ParameterValues;

        foreach (var field in attempt.ModelConfig.ParameterFields.Where(field => field.CallDefined && field.Capability is "SupportsToolCalling" or "SupportsStructuredOutput"))
        {
            if (primaryParameterValues.TryGetValue(field.Name, out var value) && IsCompatibleFieldValue(field, value))
                parameterValues[field.Name] = value;
        }

        if (HasCapability(attempt.Model, attempt.ModelConfig, "SupportsToolCalling") && primaryParameterValues.TryGetValue("selected_tools", out var selectedTools))
            parameterValues["selected_tools"] = selectedTools;

        if (
            parameterValues.TryGetValue("structured_output", out var structuredOutput)
            && string.Equals(structuredOutput, primaryParameterValues.GetValueOrDefault("structured_output"), StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(structuredOutput)
            && !string.Equals(structuredOutput, "Text", StringComparison.Ordinal)
        )
        {
            foreach (var sharedField in new[] { "structured_output_schema", "structured_output_schema_name", "structured_output_schema_description", "structured_output_configured_type" })
            {
                if (primaryParameterValues.TryGetValue(sharedField, out var value))
                    parameterValues[sharedField] = value;
            }
        }

#pragma warning disable CS0618
        return new ModelCallNodeEntity
        {
            Version = Node.Version,
            Id = Node.Id,
            NodeType = Node.NodeType,
            Title = Node.Title,
            Top = Node.Top,
            Left = Node.Left,
            Width = Node.Width,
            Height = Node.Height,
            Inputs = Node.Inputs,
            Outputs = Node.Outputs,
            Models = Node.Models,
            ModelId = definition.ModelId,
            ParameterValues = parameterValues,
            BatchOutput = Node.BatchOutput,
            DropToolCalls = Node.DropToolCalls,
            DisableStreamUser = Node.DisableStreamUser || disablePromptStream,
            DisableStreamTool = Node.DisableStreamTool,
            DisableStreamReasoning = Node.DisableStreamReasoning,
            DisableStreamAssistantText = Node.DisableStreamAssistantText,
            ToolAgUiOutputModes = new Dictionary<string, ModelCallToolAgUiOutputMode>(Node.ToolAgUiOutputModes, StringComparer.Ordinal),
            Instructions = Node.Instructions,
            Prompt = Node.Prompt,
            ChatInputPath = Node.ChatInputPath,
            ChatOutputPath = Node.ChatOutputPath,
            TextOutputPath = Node.TextOutputPath,
            ImageInputPath = Node.ImageInputPath,
            ImageOutputPath = Node.ImageOutputPath,
        };
#pragma warning restore CS0618
    }

    private static bool IsCompatibleFieldValue(FieldDescriptor field, string? value)
    {
        if (value is null)
            return !field.IsRequired;

        if (field.EnumOptions is { Count: > 0 } && !field.EnumOptions.Contains(value, StringComparer.Ordinal))
            return false;

        if (field.Type == FieldDescriptorType.Boolean && !bool.TryParse(value, out _))
            return false;

        double? numericValue = null;
        if (field.Type == FieldDescriptorType.Integer)
        {
            if (!long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integerValue))
                return false;

            numericValue = integerValue;
        }
        else if (field.Type is FieldDescriptorType.Double or FieldDescriptorType.Currency)
        {
            if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
                return false;

            numericValue = doubleValue;
        }

        if (numericValue.HasValue && field.Min.HasValue && numericValue.Value < field.Min.Value)
            return false;

        if (numericValue.HasValue && field.Max.HasValue && numericValue.Value > field.Max.Value)
            return false;

        return true;
    }

    private sealed record ModelCallAttemptResources(
        ModelCallModelDefinition Definition,
        Model Model,
        ModelConfig ModelConfig,
        Connector Connector,
        ConnectorConfig ConnectorConfig,
        IModelCaller Caller
    );

    private static string? SerializeToolCall(FunctionCallContent functionCallContent)
    {
        try
        {
            return JsonSerializer.Serialize(
                new
                {
                    functionCallContent.CallId,
                    functionCallContent.Name,
                    functionCallContent.Arguments,
                }
            );
        }
        catch
        {
            return null;
        }
    }

    private static string? SerializeToolArguments(FunctionCallContent functionCallContent)
    {
        if (functionCallContent.Arguments is null)
            return null;

        try
        {
            return JsonSerializer.Serialize(functionCallContent.Arguments);
        }
        catch
        {
            return functionCallContent.Arguments.ToString();
        }
    }

    private static string SerializeToolResult(FunctionResultContent functionResultContent)
    {
        if (functionResultContent.Result is null)
            return string.Empty;

        if (functionResultContent.Result is string resultText)
            return resultText;

        try
        {
            return JsonSerializer.Serialize(functionResultContent.Result);
        }
        catch
        {
            return functionResultContent.Result.ToString() ?? string.Empty;
        }
    }

    private sealed class ModelCallNodeProgressSink(ProcessContext processContext, Trace trace, List<Information> informations, ModelCallNodeEntity node) : IModelCallProgressSink
    {
        private readonly Dictionary<string, Information> _informationByKey = new(StringComparer.Ordinal);
        private readonly HashSet<string> _openMessageIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _reasoningTextById = new(StringComparer.Ordinal);
        private readonly HashSet<string> _openReasoningIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _toolCallArgsById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _toolNamesByToolCallId = new(StringComparer.Ordinal);
        private readonly HashSet<string> _openToolCallIds = new(StringComparer.Ordinal);
        private readonly StringBuilder _pendingAssistantText = new();
        private readonly List<StreamEvent> _streamEvents = [];

        // True when this call streams incrementally (matches BaseModelCaller.CallConfiguredAgent's routing).
        // When false the response is fetched/replayed as a whole, so instead of awaiting one progress push per
        // event we buffer the events and push them in a single batched call from PersistAsync.
        private readonly bool _liveStreaming = !(node.BatchOutput || node.IsAgUiResponseStreamSuppressed());
        private bool _hasTextUpdates;
        private bool _hasReasoningUpdates;
        private bool _hasToolCallUpdates;
        private bool _hasToolCallResultUpdates;

        public bool ResponseStarted { get; private set; }
        public bool ToolInvocationStarted { get; private set; }

        public async Task OnTextStartAsync(string messageId)
        {
            ResponseStarted = true;
            if (string.IsNullOrWhiteSpace(messageId))
                throw new SharpOMaticException("MessageId must be a non-empty string.");

            await CloseAllOpenToolCallsAsync();
            await CloseAllOpenReasoningAsync();

            if (node.DisableStreamAssistantText)
                return;

            if (!_openMessageIds.Add(messageId))
                return;

            _hasTextUpdates = true;
            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.TextStart,
                    MessageId = messageId,
                    MessageRole = StreamMessageRole.Assistant,
                }
            );
        }

        public async Task OnTextDeltaAsync(string messageId, string textDelta)
        {
            if (string.IsNullOrWhiteSpace(textDelta))
                return;

            ResponseStarted = true;
            _hasTextUpdates = true;
            await OnTextStartAsync(messageId);
            _pendingAssistantText.Append(textDelta);

            if (node.DisableStreamAssistantText)
                return;

            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.TextContent,
                    MessageId = messageId,
                    TextDelta = textDelta,
                }
            );
        }

        public async Task OnTextEndAsync(string messageId)
        {
            if (!_openMessageIds.Remove(messageId))
                return;

            if (node.DisableStreamAssistantText)
                return;

            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.TextEnd,
                    MessageId = messageId,
                }
            );
        }

        public Task OnReasoningAsync(string reasoningId, string text)
        {
            ResponseStarted = true;
            return OnReasoningCoreAsync(reasoningId, text, isStreamingUpdate: true);
        }

        public Task OnToolCallAsync(string toolCallId, string? toolName, string? argsSnapshot = null, string? parentMessageId = null, string? data = null)
        {
            ResponseStarted = true;
            _hasToolCallUpdates = true;
            return OnToolCallCoreAsync(toolCallId, toolName, argsSnapshot, parentMessageId, data);
        }

        public async Task OnToolCallResultAsync(string messageId, string toolCallId, string content)
        {
            ResponseStarted = true;
            _hasToolCallResultUpdates = true;

            await CloseAllOpenTextAsync();
            await CloseAllOpenReasoningAsync();
            await CloseToolCallAsync(toolCallId);

            if (!ShouldEmitToolStreamEvents(GetToolNameForToolCallId(toolCallId)))
                return;

            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.ToolCallResult,
                    MessageId = messageId,
                    ToolCallId = toolCallId,
                    TextDelta = content,
                }
            );
        }

        public Task OnToolInvocationStartedAsync(string? toolName)
        {
            ResponseStarted = true;
            ToolInvocationStarted = true;
            return Task.CompletedTask;
        }

        public async Task CompleteAsync()
        {
            foreach (var messageId in _openMessageIds.ToArray())
                await OnTextEndAsync(messageId);

            await FlushPendingAssistantInformationAsync();

            foreach (var reasoningId in _openReasoningIds.ToArray())
                await CloseReasoningAsync(reasoningId);

            foreach (var toolCallId in _openToolCallIds.ToArray())
                await CloseToolCallAsync(toolCallId);
        }

        public async Task PersistAsync()
        {
            if (_streamEvents.Count == 0)
                return;

            var compacted = CompactStreamEvents(_streamEvents);
            await processContext.RepositoryService.AppendStreamEvents(compacted);

            // In live-streaming mode each event was already pushed to progress services as it was produced.
            // In batch mode nothing has been pushed yet, so emit the whole compacted set in one call rather
            // than N awaited round-trips.
            if (_liveStreaming)
                return;

            List<StreamEventProgressItem> updates = compacted.Select(streamEvent => new StreamEventProgressItem() { Event = streamEvent, Silent = false }).ToList();
            foreach (var progressService in processContext.ProgressServices)
                await progressService.StreamEventProgress(processContext.Run, updates);
        }

        public async Task ApplyBatchResponseFallbackAsync(IList<ChatMessage> responses)
        {
            var batchSeed = processContext.PeekNextStreamSequence();

            if (!_hasTextUpdates && !_hasReasoningUpdates && !_hasToolCallUpdates && !_hasToolCallResultUpdates)
            {
                await ReplayBatchResponsesAsync(responses, batchSeed);
                return;
            }

            if (!_hasReasoningUpdates)
            {
                var assistantIndex = 0;
                foreach (var response in responses)
                {
                    if (response.Role != ChatRole.Assistant)
                        continue;

                    foreach (var content in response.Contents)
                    {
                        if (content is not TextReasoningContent reasoningContent)
                            continue;

                        await OnReasoningCoreAsync(BuildBatchAssistantMessageId(batchSeed, assistantIndex), reasoningContent.Text, isStreamingUpdate: false);
                    }

                    assistantIndex += 1;
                }
            }

            if (!_hasToolCallUpdates)
            {
                var assistantIndex = 0;
                foreach (var response in responses)
                {
                    if (response.Role != ChatRole.Assistant)
                        continue;

                    var toolCallIndex = 0;
                    foreach (var content in response.Contents)
                    {
                        if (content is not FunctionCallContent functionCallContent)
                            continue;

                        await OnToolCallCoreAsync(
                            functionCallContent.CallId?.Trim() ?? BuildBatchToolCallId(batchSeed, assistantIndex, toolCallIndex),
                            functionCallContent.Name,
                            SerializeToolArguments(functionCallContent),
                            BuildBatchAssistantMessageId(batchSeed, assistantIndex),
                            SerializeToolCall(functionCallContent)
                        );
                        await CloseToolCallAsync(functionCallContent.CallId?.Trim() ?? BuildBatchToolCallId(batchSeed, assistantIndex, toolCallIndex));
                        toolCallIndex += 1;
                    }

                    assistantIndex += 1;
                }
            }

            if (!_hasToolCallResultUpdates)
            {
                var responseIndex = 0;
                foreach (var response in responses)
                {
                    var resultIndex = 0;
                    foreach (var content in response.Contents)
                    {
                        if (content is not FunctionResultContent functionResultContent)
                            continue;

                        await OnToolCallResultAsync(
                            BuildBatchToolResultMessageId(batchSeed, responseIndex, resultIndex),
                            functionResultContent.CallId?.Trim() ?? BuildBatchToolResultCallId(batchSeed, responseIndex, resultIndex),
                            SerializeToolResult(functionResultContent)
                        );
                        resultIndex += 1;
                    }

                    responseIndex += 1;
                }
            }
        }

        private async Task OnReasoningCoreAsync(string reasoningId, string text, bool isStreamingUpdate)
        {
            _hasReasoningUpdates = true;
            await CloseAllOpenToolCallsAsync();
            await CloseAllOpenTextAsync();
            await FlushPendingAssistantInformationAsync();

            var reasoningText = text;
            if (!string.IsNullOrWhiteSpace(reasoningText))
                await UpsertInformationAsync(reasoningId, InformationType.Reasoning, reasoningText, data: null);
            else if (!_informationByKey.ContainsKey(reasoningId))
                await UpsertInformationAsync(reasoningId, InformationType.Reasoning, string.Empty, data: null);

            if (node.DisableStreamReasoning)
                return;

            if (string.IsNullOrWhiteSpace(reasoningText))
                return;

            await OpenReasoningAsync(reasoningId);

            _reasoningTextById.TryGetValue(reasoningId, out var currentText);
            if (string.Equals(currentText, reasoningText, StringComparison.Ordinal))
                return;

            var delta = reasoningText;
            if (isStreamingUpdate && !string.IsNullOrEmpty(currentText) && reasoningText.StartsWith(currentText, StringComparison.Ordinal))
                delta = reasoningText[currentText.Length..];

            _reasoningTextById[reasoningId] = reasoningText;

            if (string.IsNullOrWhiteSpace(delta))
                return;

            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.ReasoningMessageContent,
                    MessageId = reasoningId,
                    TextDelta = delta,
                }
            );
        }

        private async Task OnToolCallCoreAsync(string toolCallId, string? toolName, string? argsSnapshot, string? parentMessageId, string? data)
        {
            await CloseAllOpenTextAsync();
            await CloseAllOpenReasoningAsync();
            await FlushPendingAssistantInformationAsync();

            var title = string.IsNullOrWhiteSpace(toolName) ? "Tool call" : toolName;
            if (!string.IsNullOrWhiteSpace(toolName))
                _toolNamesByToolCallId[toolCallId] = toolName;

            await UpsertInformationAsync(toolCallId, InformationType.ToolCall, title, data);

            if (!ShouldEmitToolStreamEvents(toolName))
                return;

            if (_openToolCallIds.Add(toolCallId))
            {
                await AddStreamEventAsync(
                    new StreamEventWrite()
                    {
                        EventKind = StreamEventKind.ToolCallStart,
                        MessageId = toolCallId,
                        ToolCallId = toolCallId,
                        TextDelta = title,
                        ParentMessageId = parentMessageId,
                    }
                );
            }

            _toolCallArgsById.TryGetValue(toolCallId, out var currentArgs);
            if (string.Equals(currentArgs, argsSnapshot, StringComparison.Ordinal))
                return;

            var delta = argsSnapshot;
            if (!string.IsNullOrEmpty(currentArgs) && !string.IsNullOrEmpty(argsSnapshot) && argsSnapshot.StartsWith(currentArgs, StringComparison.Ordinal))
                delta = argsSnapshot[currentArgs.Length..];

            _toolCallArgsById[toolCallId] = argsSnapshot ?? string.Empty;
            if (string.IsNullOrWhiteSpace(delta))
                return;

            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.ToolCallArgs,
                    MessageId = toolCallId,
                    ToolCallId = toolCallId,
                    TextDelta = delta,
                }
            );
        }

        private async Task ReplayBatchResponsesAsync(IList<ChatMessage> responses, int batchSeed)
        {
            var assistantIndex = 0;
            var responseIndex = 0;
            foreach (var response in responses)
            {
                var assistantBaseMessageId = response.Role == ChatRole.Assistant ? BuildBatchAssistantMessageId(batchSeed, assistantIndex) : null;
                var currentAssistantMessageId = assistantBaseMessageId;
                var lastAssistantMessageId = assistantBaseMessageId;
                var assistantTextSegmentIndex = 0;
                var toolCallIndex = 0;
                var toolResultIndex = 0;

                foreach (var content in response.Contents)
                {
                    switch (content)
                    {
                        case TextContent textContent when (response.Role == ChatRole.Assistant) && !string.IsNullOrWhiteSpace(textContent.Text):
                            if ((currentAssistantMessageId is null) || !_openMessageIds.Contains(currentAssistantMessageId))
                            {
                                currentAssistantMessageId = assistantTextSegmentIndex == 0
                                    ? assistantBaseMessageId
                                    : BuildBatchAssistantTextSegmentId(batchSeed, assistantIndex, assistantTextSegmentIndex);
                                assistantTextSegmentIndex += 1;
                            }

                            lastAssistantMessageId = currentAssistantMessageId;
                            await OnTextDeltaAsync(currentAssistantMessageId!, textContent.Text);
                            break;

                        case TextReasoningContent reasoningContent when response.Role == ChatRole.Assistant:
                            await OnReasoningCoreAsync(lastAssistantMessageId ?? assistantBaseMessageId!, reasoningContent.Text, isStreamingUpdate: false);
                            break;

                        case FunctionCallContent functionCallContent:
                            var toolCallId = functionCallContent.CallId?.Trim() ?? BuildBatchToolCallId(batchSeed, assistantIndex, toolCallIndex);
                            await OnToolCallCoreAsync(
                                toolCallId,
                                functionCallContent.Name,
                                SerializeToolArguments(functionCallContent),
                                lastAssistantMessageId,
                                SerializeToolCall(functionCallContent)
                            );
                            await CloseToolCallAsync(toolCallId);
                            toolCallIndex += 1;
                            break;

                        case FunctionResultContent functionResultContent:
                            await OnToolCallResultAsync(
                                BuildBatchToolResultMessageId(batchSeed, responseIndex, toolResultIndex),
                                functionResultContent.CallId?.Trim() ?? BuildBatchToolResultCallId(batchSeed, responseIndex, toolResultIndex),
                                SerializeToolResult(functionResultContent)
                            );
                            toolResultIndex += 1;
                            break;
                    }
                }

                if (response.Role == ChatRole.Assistant)
                {
                    await CloseAllOpenTextAsync();
                    await CloseAllOpenReasoningAsync();
                    assistantIndex += 1;
                }

                responseIndex += 1;
            }
        }

        private static string BuildBatchAssistantMessageId(int batchSeed, int assistantIndex)
        {
            return $"assistant:batch:{batchSeed}:{assistantIndex}";
        }

        private static string BuildBatchAssistantTextSegmentId(int batchSeed, int assistantIndex, int assistantTextSegmentIndex)
        {
            return $"{BuildBatchAssistantMessageId(batchSeed, assistantIndex)}:text:{assistantTextSegmentIndex}";
        }

        private static string BuildBatchToolCallId(int batchSeed, int assistantIndex, int toolCallIndex)
        {
            return $"tool:batch:{batchSeed}:{assistantIndex}:{toolCallIndex}";
        }

        private static string BuildBatchToolResultMessageId(int batchSeed, int responseIndex, int toolResultIndex)
        {
            return $"tool-result:batch:{batchSeed}:{responseIndex}:{toolResultIndex}";
        }

        private static string BuildBatchToolResultCallId(int batchSeed, int responseIndex, int toolResultIndex)
        {
            return $"tool-call:batch:{batchSeed}:{responseIndex}:{toolResultIndex}";
        }

        private async Task OpenReasoningAsync(string reasoningId)
        {
            if (node.DisableStreamReasoning)
                return;

            if (!_openReasoningIds.Add(reasoningId))
                return;

            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.ReasoningStart,
                    MessageId = reasoningId,
                }
            );
            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.ReasoningMessageStart,
                    MessageId = reasoningId,
                    MessageRole = StreamMessageRole.Reasoning,
                }
            );
        }

        private async Task CloseAllOpenTextAsync()
        {
            foreach (var messageId in _openMessageIds.ToArray())
                await OnTextEndAsync(messageId);
        }

        private async Task CloseAllOpenReasoningAsync()
        {
            foreach (var reasoningId in _openReasoningIds.ToArray())
                await CloseReasoningAsync(reasoningId);
        }

        private async Task CloseAllOpenToolCallsAsync()
        {
            foreach (var toolCallId in _openToolCallIds.ToArray())
                await CloseToolCallAsync(toolCallId);
        }

        private async Task CloseReasoningAsync(string reasoningId)
        {
            if (!_openReasoningIds.Remove(reasoningId))
                return;

            if (node.DisableStreamReasoning)
                return;

            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.ReasoningMessageEnd,
                    MessageId = reasoningId,
                }
            );
            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.ReasoningEnd,
                    MessageId = reasoningId,
                }
            );
        }

        private async Task CloseToolCallAsync(string toolCallId)
        {
            if (!_openToolCallIds.Remove(toolCallId))
                return;

            if (!ShouldEmitToolStreamEvents(GetToolNameForToolCallId(toolCallId)))
                return;

            await AddStreamEventAsync(
                new StreamEventWrite()
                {
                    EventKind = StreamEventKind.ToolCallEnd,
                    MessageId = toolCallId,
                    ToolCallId = toolCallId,
                }
            );
        }

        private string? GetToolNameForToolCallId(string toolCallId)
        {
            return _toolNamesByToolCallId.TryGetValue(toolCallId, out var toolName)
                ? toolName
                : null;
        }

        private bool ShouldEmitToolStreamEvents(string? toolName)
        {
            return ResolveToolAgUiOutputMode(toolName) switch
            {
                ModelCallToolAgUiOutputMode.Always => true,
                ModelCallToolAgUiOutputMode.Never => false,
                _ => !node.DisableStreamTool,
            };
        }

        private ModelCallToolAgUiOutputMode ResolveToolAgUiOutputMode(string? toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return ModelCallToolAgUiOutputMode.Inherit;

            var modes = node.ToolAgUiOutputModes;
            return (modes is not null) && modes.TryGetValue(toolName.Trim(), out var mode)
                ? mode
                : ModelCallToolAgUiOutputMode.Inherit;
        }

        private async Task UpsertInformationAsync(string key, InformationType informationType, string text, string? data)
        {
            if (!_informationByKey.TryGetValue(key, out var information))
            {
                information = new Information()
                {
                    InformationId = Guid.NewGuid(),
                    RunId = trace.RunId,
                    TraceId = trace.TraceId,
                    Created = DateTime.Now,
                    InformationType = informationType,
                    Text = text,
                    Data = data,
                };

                _informationByKey[key] = information;
                informations.Add(information);
            }
            else
            {
                if ((information.Text == text) && (information.Data == data))
                    return;

                information.Text = text;
                information.Data = data;
            }

            await PublishInformationChangesAsync([information]);
        }

        private async Task FlushPendingAssistantInformationAsync()
        {
            var assistantText = _pendingAssistantText.ToString();
            _pendingAssistantText.Clear();

            if (string.IsNullOrWhiteSpace(assistantText))
                return;

            var information = new Information()
            {
                InformationId = Guid.NewGuid(),
                RunId = trace.RunId,
                TraceId = trace.TraceId,
                Created = DateTime.Now,
                InformationType = InformationType.Assistant,
                Text = assistantText,
                Data = null,
            };

            informations.Add(information);
            await PublishInformationChangesAsync([information]);
        }

        private async Task PublishInformationChangesAsync(List<Information> changes)
        {
            foreach (var progressService in processContext.ProgressServices)
                await progressService.InformationsProgress(processContext.Run, changes);
        }

        private async Task AddStreamEventAsync(StreamEventWrite write)
        {
            var streamEvent = new StreamEvent()
            {
                StreamEventId = Guid.NewGuid(),
                RunId = processContext.Run.RunId,
                WorkflowId = processContext.Run.WorkflowId,
                ConversationId = processContext.StreamConversationId,
                SequenceNumber = processContext.AllocateNextStreamSequence(),
                Created = DateTime.UtcNow,
                EventKind = write.EventKind,
                MessageId = write.MessageId,
                MessageRole = write.MessageRole,
                ActivityType = write.ActivityType,
                Replace = write.Replace,
                TextDelta = write.TextDelta,
                ToolCallId = write.ToolCallId,
                ParentMessageId = write.ParentMessageId,
                Metadata = write.Metadata,
                HideFromReply = false,
            };

            _streamEvents.Add(streamEvent);

            // Batch mode defers all pushes to a single call in PersistAsync; only live streaming pushes per event.
            if (!_liveStreaming)
                return;

            List<StreamEventProgressItem> updates = [new StreamEventProgressItem() { Event = streamEvent, Silent = false }];
            foreach (var progressService in processContext.ProgressServices)
                await progressService.StreamEventProgress(processContext.Run, updates);
        }

        private static List<StreamEvent> CompactStreamEvents(List<StreamEvent> streamEvents)
        {
            if (streamEvents.Count <= 1)
                return streamEvents;

            List<StreamEvent> compacted = [];

            for (var i = 0; i < streamEvents.Count; i += 1)
            {
                var streamEvent = streamEvents[i];
                if (!CanCompactContent(streamEvent))
                {
                    compacted.Add(streamEvent);
                    continue;
                }

                var combinedValue = streamEvent.TextDelta ?? string.Empty;
                while (
                    (i + 1 < streamEvents.Count)
                    && CanCompactContent(streamEvents[i + 1])
                    && (streamEvents[i + 1].EventKind == streamEvent.EventKind)
                    && string.Equals(GetCompactionKey(streamEvents[i + 1]), GetCompactionKey(streamEvent), StringComparison.Ordinal)
                )
                {
                    i += 1;
                    combinedValue += streamEvents[i].TextDelta ?? string.Empty;
                }

                streamEvent.TextDelta = combinedValue;
                compacted.Add(streamEvent);
            }

            return compacted;
        }

        private static bool CanCompactContent(StreamEvent streamEvent)
        {
            return
                (
                    (!string.IsNullOrWhiteSpace(streamEvent.MessageId)
                        && (streamEvent.EventKind == StreamEventKind.TextContent || streamEvent.EventKind == StreamEventKind.ReasoningMessageContent || streamEvent.EventKind == StreamEventKind.ToolCallArgs))
                );
        }

        private static string? GetCompactionKey(StreamEvent streamEvent)
        {
            return streamEvent.MessageId;
        }
    }
}
