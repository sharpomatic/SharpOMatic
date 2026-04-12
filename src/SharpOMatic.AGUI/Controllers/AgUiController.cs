using SharpOMatic.AGUI.DTO;
using SharpOMatic.AGUI.Services;

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

    private static ContextMergeResumeInput BuildResumeInput(AgUiRunRequest request, string threadId, string protocolRunId)
    {
        var agentPayload = new Dictionary<string, object?>();
        AddJsonProperty(agentPayload, "latestUserMessage", FindLatestUserMessage(request.Messages));
        AddJsonProperty(agentPayload, "allMessages", request.Messages);
        AddJsonProperty(agentPayload, "state", request.State);
        AddJsonProperty(agentPayload, "context", request.Context);

        var agentRoot = ContextHelpers.FastDeserializeString(JsonSerializer.Serialize(agentPayload)) as ContextObject
            ?? throw new SharpOMaticException("AG-UI request payload could not be converted into workflow context.");

        ContextObject root = [];
        root.Set("agent", agentRoot);
        return new ContextMergeResumeInput() { Context = root };
    }

    private async Task WriteStreamEventAsync(StreamEvent streamEvent)
    {
        if (string.IsNullOrWhiteSpace(streamEvent.MessageId))
            return;

        switch (streamEvent.EventKind)
        {
            case StreamEventKind.TextStart:
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_START",
                    messageId = streamEvent.MessageId,
                    role = MapRole(streamEvent.MessageRole)
                });
                break;
            case StreamEventKind.TextContent:
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_CONTENT",
                    messageId = streamEvent.MessageId,
                    delta = streamEvent.TextDelta ?? string.Empty
                });
                break;
            case StreamEventKind.TextEnd:
                await WriteEventAsync(new
                {
                    type = "TEXT_MESSAGE_END",
                    messageId = streamEvent.MessageId
                });
                break;
        }
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

    private static JsonElement? FindLatestUserMessage(JsonElement? messages)
    {
        if (!messages.HasValue || messages.Value.ValueKind != JsonValueKind.Array)
            return null;

        var items = messages.Value.EnumerateArray().ToList();
        for (var index = items.Count - 1; index >= 0; index -= 1)
        {
            var message = items[index];
            if (!message.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String)
                continue;

            if (string.Equals(role.GetString(), "user", StringComparison.OrdinalIgnoreCase))
                return message;
        }

        return null;
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
