
namespace SharpOMatic.AGUI.Controllers;

[ApiController]
[Route("agent/agui")]
public sealed class AgUiController(IEngineService engineService, IAgUiRunEventBroker broker) : ControllerBase
{
    [HttpPost]
    public async Task Post([FromBody] AgUiRunRequest request)
    {
        ConfigureSseResponse(Response);

        var threadId = request.ThreadId?.Trim();
        var protocolRunId = NormalizeProtocolRunId(request.RunId);

        try
        {
            if (string.IsNullOrWhiteSpace(threadId))
                throw new SharpOMaticException("AG-UI threadId is required.");

            var workflowId = await ResolveWorkflowId(request);
            var resumeInput = BuildResumeInput(request, threadId, protocolRunId);
            var engineRunId = await engineService.StartOrResumeConversationAndNotify(
                workflowId,
                threadId,
                resumeInput: resumeInput,
                streamConversationId: threadId
            );

            await WriteEventAsync(new
            {
                type = "RUN_STARTED",
                threadId,
                runId = protocolRunId
            });

            await foreach (var update in broker.Subscribe(engineRunId, HttpContext.RequestAborted))
            {
                if (update.StreamEvents is not null)
                {
                    foreach (var streamEvent in update.StreamEvents)
                        await WriteStreamEventAsync(streamEvent);
                }

                if (update.Run is null)
                    continue;

                if (update.Run.RunStatus is RunStatus.Success or RunStatus.Suspended)
                {
                    await WriteEventAsync(new
                    {
                        type = "RUN_FINISHED",
                        threadId,
                        runId = protocolRunId
                    });
                    break;
                }

                if (update.Run.RunStatus == RunStatus.Failed)
                {
                    await WriteEventAsync(new
                    {
                        type = "RUN_ERROR",
                        threadId,
                        runId = protocolRunId,
                        message = update.Run.Error ?? "Workflow failed."
                    });
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected.
        }
        catch (Exception ex)
        {
            await WriteEventAsync(new
            {
                type = "RUN_ERROR",
                threadId,
                runId = protocolRunId,
                message = ex.Message
            });
        }
    }

    private async Task<Guid> ResolveWorkflowId(AgUiRunRequest request)
    {
        var selector = request.ForwardedProps;
        if (!selector.HasValue || selector.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new SharpOMaticException("AG-UI forwardedProps must specify either workflowId or workflowName.");

        var workflowSelector = selector.Value;
        if (workflowSelector.ValueKind != JsonValueKind.Object)
            throw new SharpOMaticException("AG-UI forwardedProps must be a JSON object.");

        if (workflowSelector.TryGetProperty("sharpomatic", out var scopedSelector) && scopedSelector.ValueKind == JsonValueKind.Object)
            workflowSelector = scopedSelector;

        var workflowId = TryReadStringProperty(workflowSelector, "workflowId");
        var workflowName = TryReadStringProperty(workflowSelector, "workflowName");

        if (string.IsNullOrWhiteSpace(workflowId) == string.IsNullOrWhiteSpace(workflowName))
            throw new SharpOMaticException("Specify exactly one of workflowId or workflowName in AG-UI forwardedProps.");

        if (!string.IsNullOrWhiteSpace(workflowId))
        {
            if (!Guid.TryParse(workflowId, out var parsedWorkflowId))
                throw new SharpOMaticException("AG-UI workflowId must be a valid GUID.");

            return parsedWorkflowId;
        }

        return await engineService.GetWorkflowId(workflowName!);
    }

    private static AgUiAgentResumeInput BuildResumeInput(AgUiRunRequest request, string threadId, string protocolRunId)
    {
        var agentPayload = new Dictionary<string, object?>();
        AddJsonProperty(agentPayload, "latestUserMessage", FindLatestUserMessage(request.Messages));
        AddJsonProperty(agentPayload, "latestToolResult", FindLatestToolResult(request.Messages));
        AddJsonProperty(agentPayload, "messages", request.Messages);
        AddJsonProperty(agentPayload, "state", request.State);
        AddJsonProperty(agentPayload, "context", request.Context);

        var agentRoot = ContextHelpers.FastDeserializeString(JsonSerializer.Serialize(agentPayload)) as ContextObject
            ?? throw new SharpOMaticException("AG-UI request payload could not be converted into workflow context.");

        return new AgUiAgentResumeInput() { Agent = agentRoot };
    }

    private async Task WriteStreamEventAsync(StreamEventProgressItem streamEventUpdate)
    {
        if (streamEventUpdate.Silent)
            return;

        var streamEvent = streamEventUpdate.Event;
        var toolCallId = string.IsNullOrWhiteSpace(streamEvent.ToolCallId) ? streamEvent.MessageId : streamEvent.ToolCallId;
        var reasoningMessageId = GetProtocolReasoningMessageId(streamEvent.MessageId);
        var toolMessageId = GetProtocolToolMessageId(streamEvent.MessageId);

        switch (streamEvent.EventKind)
        {
            case StreamEventKind.TextStart when !string.IsNullOrWhiteSpace(streamEvent.MessageId):
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_START",
                    messageId = streamEvent.MessageId,
                    role = MapRole(streamEvent.MessageRole)
                });
                break;
            case StreamEventKind.TextContent when !string.IsNullOrWhiteSpace(streamEvent.MessageId):
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_CONTENT",
                    messageId = streamEvent.MessageId,
                    delta = streamEvent.TextDelta ?? string.Empty
                });
                break;
            case StreamEventKind.TextEnd when !string.IsNullOrWhiteSpace(streamEvent.MessageId):
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId = streamEvent.MessageId
                });
                break;
            case StreamEventKind.ReasoningStart when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_START",
                    messageId = reasoningMessageId
                });
                break;
            case StreamEventKind.ReasoningMessageStart when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_MESSAGE_START",
                    messageId = reasoningMessageId,
                    role = MapRole(streamEvent.MessageRole)
                });
                break;
            case StreamEventKind.ReasoningMessageContent when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_MESSAGE_CONTENT",
                    messageId = reasoningMessageId,
                    delta = streamEvent.TextDelta ?? string.Empty
                });
                break;
            case StreamEventKind.ReasoningMessageEnd when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_MESSAGE_END",
                    messageId = reasoningMessageId
                });
                break;
            case StreamEventKind.ReasoningEnd when reasoningMessageId is not null:
                await WriteEventAsync(new
                {
                    type = "REASONING_END",
                    messageId = reasoningMessageId
                });
                break;
            case StreamEventKind.ToolCallStart when !string.IsNullOrWhiteSpace(toolCallId):
                await WriteEventAsync(new
                {
                    type = "TOOL_CALL_START",
                    toolCallId,
                    toolCallName = streamEvent.TextDelta ?? string.Empty,
                    parentMessageId = streamEvent.ParentMessageId
                });
                break;
            case StreamEventKind.ToolCallArgs when !string.IsNullOrWhiteSpace(toolCallId):
                await WriteEventAsync(new
                {
                    type = "TOOL_CALL_ARGS",
                    toolCallId,
                    delta = streamEvent.TextDelta ?? string.Empty
                });
                break;
            case StreamEventKind.ToolCallEnd when !string.IsNullOrWhiteSpace(toolCallId):
                await WriteEventAsync(new
                {
                    type = "TOOL_CALL_END",
                    toolCallId
                });
                break;
            case StreamEventKind.ToolCallResult when toolMessageId is not null && !string.IsNullOrWhiteSpace(toolCallId):
                await WriteEventAsync(new
                {
                    type = "TOOL_CALL_RESULT",
                    messageId = toolMessageId,
                    toolCallId,
                    content = streamEvent.TextDelta ?? string.Empty,
                    role = "tool"
                });
                break;
        }
    }

    private static string? GetProtocolReasoningMessageId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        return $"reason:{messageId.Trim()}";
    }

    private static string? GetProtocolToolMessageId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        return $"tool:{messageId.Trim()}";
    }

    private static void AddJsonProperty(IDictionary<string, object?> payload, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            payload[propertyName] = value.Trim();
    }

    private static void AddJsonProperty(IDictionary<string, object?> payload, string propertyName, JsonElement? value)
    {
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return;

        payload[propertyName] = value.Value;
    }

    private static void AddJsonProperty(IDictionary<string, object?> payload, string propertyName, object? value)
    {
        if (value is null)
            return;

        payload[propertyName] = value;
    }

    private static JsonElement? FindLatestUserMessage(JsonElement? messages)
    {
        var latestMessage = GetLatestMessage(messages);
        if (!latestMessage.HasValue)
            return null;

        var message = latestMessage.Value;
        if (!HasRole(message, "user"))
            return null;

        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            return null;

        return message;
    }

    private static object? FindLatestToolResult(JsonElement? messages)
    {
        var latestMessage = GetLatestMessage(messages);
        if (!latestMessage.HasValue)
            return null;

        if (!HasRole(latestMessage.Value, "tool"))
            return null;

        return ConvertLatestToolResult(latestMessage.Value);
    }

    private static Dictionary<string, object?> ConvertLatestToolResult(JsonElement message)
    {
        Dictionary<string, object?> payload = [];

        foreach (var property in message.EnumerateObject())
        {
            payload[property.Name] = property.Value;
        }

        if (message.TryGetProperty("content", out var content))
            AddLatestToolResultValue(payload, content);

        return payload;
    }

    private static void AddLatestToolResultValue(IDictionary<string, object?> payload, JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.String)
            return;

        var rawContent = content.GetString();
        if (string.IsNullOrWhiteSpace(rawContent))
            return;

        payload["value"] = ContextHelpers.FastDeserializeString(rawContent);
    }

    private static JsonElement? GetLatestMessage(JsonElement? messages)
    {
        if (!messages.HasValue || messages.Value.ValueKind != JsonValueKind.Array)
            return null;

        JsonElement? latestMessage = null;
        foreach (var message in messages.Value.EnumerateArray())
            latestMessage = message;

        if (!latestMessage.HasValue || latestMessage.Value.ValueKind != JsonValueKind.Object)
            return null;

        return latestMessage;
    }

    private static bool HasRole(JsonElement message, string expectedRole)
    {
        if (!message.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String)
            return false;

        return string.Equals(role.GetString(), expectedRole, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProtocolRunId(string? runId)
    {
        return string.IsNullOrWhiteSpace(runId)
            ? Guid.NewGuid().ToString("N")
            : runId.Trim();
    }

    private static string? TryReadStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyValue) || propertyValue.ValueKind != JsonValueKind.String)
            return null;

        return propertyValue.GetString()?.Trim();
    }

    private static string MapRole(StreamMessageRole? role)
    {
        return role switch
        {
            StreamMessageRole.User => "user",
            StreamMessageRole.System => "system",
            StreamMessageRole.Reasoning => "reasoning",
            _ => "assistant",
        };
    }

    private static void ConfigureSseResponse(HttpResponse response)
    {
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("X-Accel-Buffering", "no");
        response.ContentType = "text/event-stream";
    }

    private async Task WriteEventAsync(object payload)
    {
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n");
        await Response.Body.FlushAsync(HttpContext.RequestAborted);
    }
}
