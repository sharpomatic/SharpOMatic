#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.ModelCall)]
public class ModelCallNode(ThreadContext threadContext, ModelCallNodeEntity node) : RunNode<ModelCallNodeEntity>(threadContext, node)
{
    protected override async Task<NodeExecutionResult> RunInternal()
    {
        // Validate and load the model and connector instances
        (var model, var modelConfig, var connector, var connectorConfig) = await LoadModelAndConnector();
        var progressSink = new ModelCallNodeProgressSink(ProcessContext, Trace, Informations, Node);

        // Get the implementation for the specific connector config
        var caller = ProcessContext.ServiceScope.ServiceProvider.GetKeyedService<IModelCaller>(connectorConfig.ConfigId);
        if (caller is null)
            throw new SharpOMaticException($"No implementation found for connector '{connectorConfig.ConfigId}'");

        // Use specific implementation to perform call for us
        (var chat, var responses, var tempContext) = await caller.Call(model, modelConfig, connector, connectorConfig, ProcessContext, ThreadContext, Node, progressSink);
        await progressSink.ApplyBatchResponseFallbackAsync(responses);
        await progressSink.CompleteAsync();

        // Merge result context into the nodes context
        ProcessContext.MergeContexts(ThreadContext.NodeContext, tempContext);

        // If there is an output path for placing new chat history
        if (!string.IsNullOrWhiteSpace(Node.ChatOutputPath))
        {
            ContextList chatList = [];
            chatList.AddRange(chat);

            foreach (var message in responses)
                chatList.Add(message);

            ThreadContext.NodeContext.TrySet(Node.ChatOutputPath, chatList);
        }

        await progressSink.PersistAsync();

        return NodeExecutionResult.Continue($"{model.Name ?? "(empty)"}", ResolveOptionalSingleOutput(ThreadContext));
    }

    private async Task<(Model, ModelConfig, Connector, ConnectorConfig)> LoadModelAndConnector()
    {
        if (Node.ModelId is null)
            throw new SharpOMaticException("No model selected");

        var model = await ProcessContext.RepositoryService.GetModel(Node.ModelId.Value);
        if (model is null)
            throw new SharpOMaticException("Cannot find model");

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

        return (model, modelConfig, connector, connectorConfig);
    }

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
        private readonly HashSet<string> _openToolCallIds = new(StringComparer.Ordinal);
        private readonly List<StreamEvent> _streamEvents = [];
        private bool _hasTextUpdates;
        private bool _hasReasoningUpdates;
        private bool _hasToolCallUpdates;
        private bool _hasToolCallResultUpdates;

        public async Task OnTextStartAsync(string messageId)
        {
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

            _hasTextUpdates = true;
            await OnTextStartAsync(messageId);

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
            return OnReasoningCoreAsync(reasoningId, text, isStreamingUpdate: true);
        }

        public Task OnToolCallAsync(string toolCallId, string? toolName, string? argsSnapshot = null, string? parentMessageId = null, string? data = null)
        {
            _hasToolCallUpdates = true;
            return OnToolCallCoreAsync(toolCallId, toolName, argsSnapshot, parentMessageId, data);
        }

        public async Task OnToolCallResultAsync(string messageId, string toolCallId, string content)
        {
            _hasToolCallResultUpdates = true;

            await CloseAllOpenTextAsync();
            await CloseAllOpenReasoningAsync();
            await CloseToolCallAsync(toolCallId);

            if (node.DisableStreamTool)
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

        public async Task CompleteAsync()
        {
            foreach (var messageId in _openMessageIds.ToArray())
                await OnTextEndAsync(messageId);

            foreach (var reasoningId in _openReasoningIds.ToArray())
                await CloseReasoningAsync(reasoningId);

            foreach (var toolCallId in _openToolCallIds.ToArray())
                await CloseToolCallAsync(toolCallId);
        }

        public Task PersistAsync()
        {
            return _streamEvents.Count == 0
                ? Task.CompletedTask
                : processContext.RepositoryService.AppendStreamEvents(CompactStreamEvents(_streamEvents));
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

            var reasoningText = text;
            if (!string.IsNullOrWhiteSpace(reasoningText))
                await UpsertInformationAsync(reasoningId, InformationType.Reasoning, reasoningText, data: null);

            if (node.DisableStreamReasoning)
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

            var title = string.IsNullOrWhiteSpace(toolName) ? "Tool call" : toolName;
            await UpsertInformationAsync(toolCallId, InformationType.ToolCall, title, data);

            if (node.DisableStreamTool)
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

            if (node.DisableStreamTool)
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

            List<Information> changes = [information];
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
                TextDelta = write.TextDelta,
                ToolCallId = write.ToolCallId,
                ParentMessageId = write.ParentMessageId,
                Metadata = write.Metadata,
            };

            _streamEvents.Add(streamEvent);

            List<StreamEvent> updates = [streamEvent];
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
