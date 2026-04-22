namespace SharpOMatic.Engine.Helpers;

public class StreamEventHelper
{
    private const string ActivityStatePath = "_hidden.activity";
    private const string ActivityInstanceNameField = "instanceName";
    private const string ActivityTypeField = "activityType";
    private const string ActivityContentField = "content";
    private const string AgentStatePath = "agent.state";
    private const string StateSyncContentPath = "agent._hidden.state";

    private readonly ProcessContext _processContext;
    private readonly ContextObject _context;

    public StreamEventHelper(ProcessContext processContext, ContextObject context)
    {
        _processContext = processContext ?? throw new ArgumentNullException(nameof(processContext));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<List<StreamEvent>> AddTextStartAsync(StreamMessageRole role, string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextStart,
                MessageId = RequireMessageId(messageId),
                MessageRole = role,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddTextContentAsync(string messageId, string delta, string? metadata = null, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(delta))
            throw new SharpOMaticException("Text content delta cannot be empty or whitespace.");

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextContent,
                MessageId = RequireMessageId(messageId),
                TextDelta = delta,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddTextEndAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextEnd,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task AddTextMessageAsync(StreamMessageRole role, string messageId, string text, string? metadata = null, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new SharpOMaticException("Text message cannot be empty or whitespace.");

        messageId = RequireMessageId(messageId);

        await AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextStart,
                MessageId = messageId,
                MessageRole = role,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextContent,
                MessageId = messageId,
                TextDelta = text,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextEnd,
                MessageId = messageId,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningStartAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningStart,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningMessageStartAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageStart,
                MessageId = RequireMessageId(messageId),
                MessageRole = StreamMessageRole.Reasoning,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningMessageContentAsync(string messageId, string delta, string? metadata = null, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(delta))
            throw new SharpOMaticException("Reasoning message delta cannot be empty or whitespace.");

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageContent,
                MessageId = RequireMessageId(messageId),
                TextDelta = delta,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningMessageEndAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageEnd,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningEndAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningEnd,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task AddReasoningMessageAsync(string messageId, string text, string? metadata = null, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new SharpOMaticException("Reasoning message cannot be empty or whitespace.");

        messageId = RequireMessageId(messageId);

        await AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningStart,
                MessageId = messageId,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageStart,
                MessageId = messageId,
                MessageRole = StreamMessageRole.Reasoning,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageContent,
                MessageId = messageId,
                TextDelta = text,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageEnd,
                MessageId = messageId,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningEnd,
                MessageId = messageId,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddToolCallStartAsync(string toolCallId, string toolName, string? parentMessageId = null, string? metadata = null, bool silent = false)
    {
        toolCallId = RequireToolCallId(toolCallId);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(toolName, "Tool call name cannot be empty or whitespace."),
                ParentMessageId = NormalizeOptionalId(parentMessageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddToolCallArgsAsync(string toolCallId, string args, string? metadata = null, bool silent = false)
    {
        toolCallId = RequireToolCallId(toolCallId);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(args, "Tool call args cannot be empty or whitespace."),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddToolCallEndAsync(string toolCallId, string? metadata = null, bool silent = false)
    {
        toolCallId = RequireToolCallId(toolCallId);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddToolCallResultAsync(string resultMessageId, string toolCallId, string result, string? metadata = null, bool silent = false)
    {
        if (result is null)
            throw new SharpOMaticException("Tool call result cannot be null.");

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallResult,
                MessageId = RequireToolResultMessageId(resultMessageId),
                ToolCallId = RequireToolCallId(toolCallId),
                TextDelta = result,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task AddToolCallAsync(string toolCallId, string toolName, string args, string? parentMessageId = null, string? metadata = null, bool silent = false)
    {
        toolCallId = RequireToolCallId(toolCallId);

        await AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(toolName, "Tool call name cannot be empty or whitespace."),
                ParentMessageId = NormalizeOptionalId(parentMessageId),
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(args, "Tool call args cannot be empty or whitespace."),
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task AddToolCallWithResultAsync(string toolCallId, string toolName, string args, string resultMessageId, string result, string? parentMessageId = null, string? metadata = null, bool silent = false)
    {
        if (result is null)
            throw new SharpOMaticException("Tool call result cannot be null.");

        toolCallId = RequireToolCallId(toolCallId);
        resultMessageId = RequireToolResultMessageId(resultMessageId);

        await AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(toolName, "Tool call name cannot be empty or whitespace."),
                ParentMessageId = NormalizeOptionalId(parentMessageId),
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(args, "Tool call args cannot be empty or whitespace."),
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallResult,
                MessageId = resultMessageId,
                ToolCallId = toolCallId,
                TextDelta = result,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddActivitySnapshotAsync(string messageId, string activityType, object content, bool? replace = null, string? metadata = null, bool silent = false)
    {
        ArgumentNullException.ThrowIfNull(content);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ActivitySnapshot,
                MessageId = RequireMessageId(messageId),
                ActivityType = RequireActivityType(activityType),
                Replace = replace,
                TextDelta = SerializePlainJsonPayload(content, "Activity snapshot content must be JSON serializable."),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddActivityDeltaAsync(string messageId, string activityType, object patch, string? metadata = null, bool silent = false)
    {
        ArgumentNullException.ThrowIfNull(patch);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ActivityDelta,
                MessageId = RequireMessageId(messageId),
                ActivityType = RequireActivityType(activityType),
                TextDelta = SerializePlainJsonPayload(patch, "Activity delta patch must be JSON serializable."),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddStateSnapshotAsync(object? snapshot, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.StateSnapshot,
                TextDelta = SerializePlainJsonPayload(snapshot, "State snapshot must be JSON serializable."),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddStateDeltaAsync(object patch, string? metadata = null, bool silent = false)
    {
        ArgumentNullException.ThrowIfNull(patch);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.StateDelta,
                TextDelta = SerializePlainJsonPayload(patch, "State delta patch must be JSON serializable."),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task<List<StreamEvent>> AddActivitySyncFromContextAsync(
        string instanceName,
        string activityType,
        string contextPath,
        bool? replace = null,
        bool snapshotsOnly = false,
        string? metadata = null,
        bool silent = false
    )
    {
        instanceName = RequireActivityInstanceName(instanceName);
        activityType = RequireActivityType(activityType);
        var content = ResolveActivityContentFromContextPath(contextPath, "Activity sync");
        var snapshotJson = SerializePlainJsonPayload(content, "Activity sync content must be JSON serializable.");
        var existingEntry = FindStoredActivityState(instanceName);

        if (existingEntry is null)
        {
            var snapshotEvents = await AddActivitySnapshotJsonAsync(instanceName, activityType, snapshotJson, replace, metadata, silent);
            UpsertStoredActivityState(instanceName, activityType, content);
            return snapshotEvents;
        }

        var storedActivityType = RequireStoredActivityType(existingEntry, instanceName);
        if (!string.Equals(storedActivityType, activityType, StringComparison.Ordinal))
            throw new SharpOMaticException($"Activity instance '{instanceName}' is already associated with activity type '{storedActivityType}', not '{activityType}'.");

        if (snapshotsOnly)
        {
            var snapshotEvents = await AddActivitySnapshotJsonAsync(instanceName, storedActivityType, snapshotJson, true, metadata, silent);
            UpsertStoredActivityState(instanceName, activityType, content);
            return snapshotEvents;
        }

        var patch = BuildActivityDeltaPatch(RequireStoredActivityContent(existingEntry, instanceName), content);

        if (patch.Count == 0)
        {
            UpsertStoredActivityState(instanceName, storedActivityType, content);
            return [];
        }

        var patchJson = SerializePlainJsonPayload(patch, "Activity sync delta patch must be JSON serializable.");
        var events = GetUtf8Size(patchJson) < GetUtf8Size(snapshotJson)
            ? await AddActivityDeltaJsonAsync(instanceName, storedActivityType, patchJson, metadata, silent)
            : await AddActivitySnapshotJsonAsync(instanceName, storedActivityType, snapshotJson, true, metadata, silent);

        UpsertStoredActivityState(instanceName, activityType, content);
        return events;
    }

    public async Task<List<StreamEvent>> AddStateSyncAsync(bool snapshotsOnly = false, string? metadata = null, bool silent = false)
    {
        if (!_context.TryGet<object?>(AgentStatePath, out var currentState))
            return [];

        var clonedCurrentState = CloneJsonValue(currentState, $"State sync context path '{AgentStatePath}' could not be cloned from JSON.");
        var snapshotJson = SerializePlainJsonPayload(clonedCurrentState, "State sync content must be JSON serializable.");

        if (!_context.TryGet<object?>(StateSyncContentPath, out var existingState))
        {
            var snapshotEvents = await AddStateSnapshotJsonAsync(snapshotJson, metadata, silent);
            SetStoredStateSyncContent(clonedCurrentState);
            return snapshotEvents;
        }

        if (snapshotsOnly)
        {
            var snapshotEvents = await AddStateSnapshotJsonAsync(snapshotJson, metadata, silent);
            SetStoredStateSyncContent(clonedCurrentState);
            return snapshotEvents;
        }

        var clonedExistingState = CloneJsonValue(existingState, "Stored state sync baseline could not be cloned from JSON.");
        var patch = BuildJsonDeltaPatch(clonedExistingState, clonedCurrentState, allowRootReplace: true);

        if (patch.Count == 0)
        {
            SetStoredStateSyncContent(clonedCurrentState);
            return [];
        }

        var hasRootReplace = patch.Any(operation =>
            operation.TryGetValue("path", out var pathValue) &&
            pathValue is string path &&
            string.IsNullOrEmpty(path)
        );

        var patchJson = SerializePlainJsonPayload(patch, "State sync delta patch must be JSON serializable.");
        var events = !hasRootReplace && GetUtf8Size(patchJson) < GetUtf8Size(snapshotJson)
            ? await AddStateDeltaJsonAsync(patchJson, metadata, silent)
            : await AddStateSnapshotJsonAsync(snapshotJson, metadata, silent);

        SetStoredStateSyncContent(clonedCurrentState);
        return events;
    }

    public Task<List<StreamEvent>> AddStepStartAsync(string stepName, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.StepStart,
                TextDelta = RequireStepName(stepName),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddStepEndAsync(string stepName, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.StepEnd,
                TextDelta = RequireStepName(stepName),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    private Task<List<StreamEvent>> AddEventsAsync(params StreamEventWrite[] events)
    {
        return _processContext.AppendStreamEvents(events);
    }

    private static string RequireMessageId(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new SharpOMaticException("MessageId must be a non-empty string.");

        return messageId;
    }

    private static string RequireToolCallId(string toolCallId)
    {
        if (string.IsNullOrWhiteSpace(toolCallId))
            throw new SharpOMaticException("ToolCallId must be a non-empty string.");

        return toolCallId;
    }

    private static string RequireToolResultMessageId(string resultMessageId)
    {
        if (string.IsNullOrWhiteSpace(resultMessageId))
            throw new SharpOMaticException("Tool call result message id must be a non-empty string.");

        return resultMessageId;
    }

    private static string RequireActivityType(string activityType)
    {
        if (string.IsNullOrWhiteSpace(activityType))
            throw new SharpOMaticException("ActivityType must be a non-empty string.");

        return activityType;
    }

    private static string RequireActivityInstanceName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new SharpOMaticException("Activity instance name must be a non-empty string.");

        return instanceName;
    }

    private static string RequireStepName(string stepName)
    {
        if (string.IsNullOrWhiteSpace(stepName))
            throw new SharpOMaticException("Step name must be a non-empty string.");

        return stepName;
    }

    private static string RequireNonEmpty(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new SharpOMaticException(message);

        return value;
    }

    private static string? NormalizeOptionalId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private ContextObject ResolveActivityContentFromContextPath(string contextPath, string operationName)
    {
        contextPath = RequireNonEmpty(contextPath, $"{operationName} context path cannot be empty or whitespace.");

        if (!_context.TryGet<object?>(contextPath, out var value))
            throw new SharpOMaticException($"{operationName} context path '{contextPath}' could not be found.");

        var cloned = CloneJsonValue(value, $"{operationName} context path '{contextPath}' could not be cloned from JSON.");

        if (cloned is not ContextObject content)
            throw new SharpOMaticException($"{operationName} requires context path '{contextPath}' to resolve to a JSON object.");

        return content;
    }

    private ContextObject? FindStoredActivityState(string instanceName)
    {
        if (!_context.TryGet<ContextList>(ActivityStatePath, out var instances) || instances is null)
            return null;

        foreach (var item in instances)
        {
            if (item is not ContextObject entry)
                continue;

            if (entry.TryGet<string>(ActivityInstanceNameField, out var storedInstanceName) && string.Equals(storedInstanceName, instanceName, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private void UpsertStoredActivityState(string instanceName, string activityType, ContextObject content)
    {
        var entry = FindStoredActivityState(instanceName);
        if (entry is not null)
        {
            entry[ActivityTypeField] = activityType;
            entry[ActivityContentField] = content;
            return;
        }

        var instances = GetOrCreateStoredActivityInstances();
        instances.Add(
            new ContextObject()
            {
                [ActivityInstanceNameField] = instanceName,
                [ActivityTypeField] = activityType,
                [ActivityContentField] = content,
            }
        );
    }

    private ContextList GetOrCreateStoredActivityInstances()
    {
        if (_context.TryGet<ContextList>(ActivityStatePath, out var instances) && instances is not null)
            return instances;

        instances = [];
        _context.Set(ActivityStatePath, instances);
        return instances;
    }

    private static string RequireStoredActivityType(ContextObject entry, string instanceName)
    {
        if (!entry.TryGet<string>(ActivityTypeField, out var activityType) || string.IsNullOrWhiteSpace(activityType))
            throw new SharpOMaticException($"Stored activity instance '{instanceName}' is missing a valid activity type.");

        return activityType;
    }

    private static ContextObject RequireStoredActivityContent(ContextObject entry, string instanceName)
    {
        if (!entry.TryGet<ContextObject>(ActivityContentField, out var content) || content is null)
            throw new SharpOMaticException($"Stored activity instance '{instanceName}' is missing valid content.");

        return content;
    }

    private Task<List<StreamEvent>> AddActivitySnapshotJsonAsync(string messageId, string activityType, string contentJson, bool? replace = null, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ActivitySnapshot,
                MessageId = RequireMessageId(messageId),
                ActivityType = RequireActivityType(activityType),
                Replace = replace,
                TextDelta = contentJson,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    private Task<List<StreamEvent>> AddActivityDeltaJsonAsync(string messageId, string activityType, string patchJson, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ActivityDelta,
                MessageId = RequireMessageId(messageId),
                ActivityType = RequireActivityType(activityType),
                TextDelta = patchJson,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    private Task<List<StreamEvent>> AddStateSnapshotJsonAsync(string snapshotJson, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.StateSnapshot,
                TextDelta = snapshotJson,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    private Task<List<StreamEvent>> AddStateDeltaJsonAsync(string patchJson, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.StateDelta,
                TextDelta = patchJson,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    private List<Dictionary<string, object?>> BuildActivityDeltaPatch(ContextObject oldContent, ContextObject newContent)
    {
        return BuildJsonDeltaPatch(oldContent, newContent);
    }

    private List<Dictionary<string, object?>> BuildJsonDeltaPatch(object? oldContent, object? newContent, bool allowRootReplace = false)
    {
        return ActivityJsonPatchHelper.BuildPatch(
            SerializePlainJsonPayload(oldContent, "Stored content could not be serialized to JSON."),
            SerializePlainJsonPayload(newContent, "Content could not be serialized to JSON."),
            allowRootReplace
        );
    }

    private object? CloneJsonValue(object? value, string errorMessage)
    {
        var json = SerializePlainJsonPayload(value, errorMessage);

        try
        {
            return ContextHelpers.FastDeserializeString(json);
        }
        catch
        {
            throw new SharpOMaticException(errorMessage);
        }
    }

    private void SetStoredStateSyncContent(object? value)
    {
        _context.Set(StateSyncContentPath, value);
    }

    private string SerializePlainJsonPayload(object? payload, string errorMessage)
    {
        try
        {
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(buffer);
            WritePlainJsonValue(writer, payload);
            writer.Flush();
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }
        catch
        {
            throw new SharpOMaticException(errorMessage);
        }
    }

    private static void WritePlainJsonValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case JsonElement element:
                element.WriteTo(writer);
                return;
            case JsonDocument document:
                document.RootElement.WriteTo(writer);
                return;
            case ContextObject contextObject:
                writer.WriteStartObject();
                foreach (var entry in contextObject.Snapshot())
                {
                    writer.WritePropertyName(entry.Key);
                    WritePlainJsonValue(writer, entry.Value);
                }
                writer.WriteEndObject();
                return;
            case ContextList contextList:
                writer.WriteStartArray();
                foreach (var item in contextList.Snapshot())
                    WritePlainJsonValue(writer, item);
                writer.WriteEndArray();
                return;
            case IDictionary<string, object?> dictionary:
                writer.WriteStartObject();
                foreach (var entry in dictionary)
                {
                    writer.WritePropertyName(entry.Key);
                    WritePlainJsonValue(writer, entry.Value);
                }
                writer.WriteEndObject();
                return;
            case IEnumerable enumerable when value is not string:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                    WritePlainJsonValue(writer, item);
                writer.WriteEndArray();
                return;
            default:
                JsonSerializer.Serialize(writer, value, value.GetType());
                return;
        }
    }

    private static int GetUtf8Size(string value)
    {
        return Encoding.UTF8.GetByteCount(value);
    }

}
