namespace SharpOMatic.Engine.Services;

public class RepositoryService(IDbContextFactory<SharpOMaticDbContext> dbContextFactory, IAssetStore? assetStore = null) : IRepositoryService
{
    private const string SECRET_OBFUSCATION = "********";

    private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new NodeEntityConverter() } };

    // ------------------------------------------------
    // Workflow Operations
    // ------------------------------------------------
    public async Task<List<WorkflowSummary>> GetWorkflowSummaries()
    {
        return await GetWorkflowSummaries(null, WorkflowSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetWorkflowSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var workflows = ApplyWorkflowSearch(dbContext.Workflows.AsNoTracking(), search);
        return await workflows.CountAsync();
    }

    public async Task<List<WorkflowSummary>> GetWorkflowSummaries(string? search, WorkflowSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflows = ApplyWorkflowSearch(dbContext.Workflows.AsNoTracking(), search);
        var sorted = GetSortedWorkflows(workflows, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(workflow => new WorkflowSummary
            {
                Version = workflow.Version,
                Id = workflow.WorkflowId,
                Name = workflow.Named,
                Description = workflow.Description,
                IsConversationEnabled = workflow.IsConversationEnabled,
            })
            .ToListAsync();
    }

    public async Task<WorkflowEntity> GetWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflow = await (from w in dbContext.Workflows where w.WorkflowId == workflowId select w)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return workflow is null
            ? throw new SharpOMaticException($"Workflow '{workflowId}' cannot be found.")
            : new WorkflowEntity()
            {
                Version = workflow.Version,
                Id = workflow.WorkflowId,
                Name = workflow.Named,
                Description = workflow.Description,
                IsConversationEnabled = workflow.IsConversationEnabled,
                Nodes = JsonSerializer.Deserialize<NodeEntity[]>(workflow.Nodes, _options)!,
                Connections = JsonSerializer.Deserialize<ConnectionEntity[]>(workflow.Connections, _options)!,
            };
    }

    public async Task UpsertWorkflow(WorkflowEntity workflow)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entry = await (from w in dbContext.Workflows where w.WorkflowId == workflow.Id select w).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new Workflow()
            {
                Version = workflow.Version,
                WorkflowId = workflow.Id,
                Named = "",
                Description = "",
                IsConversationEnabled = false,
                Nodes = "",
                Connections = "",
            };

            dbContext.Workflows.Add(entry);
        }

        entry.Named = workflow.Name;
        entry.Description = workflow.Description;
        entry.IsConversationEnabled = workflow.IsConversationEnabled;
        entry.Nodes = JsonSerializer.Serialize(workflow.Nodes, _options);
        entry.Connections = JsonSerializer.Serialize(workflow.Connections, _options);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflow = await (from w in dbContext.Workflows where w.WorkflowId == workflowId select w).FirstOrDefaultAsync();

        if (workflow is null)
            throw new SharpOMaticException($"Workflow '{workflowId}' cannot be found.");

        var runIds = await dbContext.Runs.Where(r => r.WorkflowId == workflowId).Select(r => r.RunId).ToListAsync();

        await DeleteAssetStorageForRuns(runIds);

        dbContext.Remove(workflow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<Guid> CopyWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var workflow = await dbContext.Workflows.AsNoTracking().FirstOrDefaultAsync(w => w.WorkflowId == workflowId);

        if (workflow is null)
            throw new SharpOMaticException($"Workflow '{workflowId}' cannot be found.");

        var existingNames = await dbContext.Workflows.AsNoTracking().Select(w => w.Named).ToListAsync();

        var nameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var baseName = workflow.Named;
        var startIndex = 1;

        if (TryParseCopySuffix(baseName, out var parsedBaseName, out var copyNumber))
        {
            baseName = parsedBaseName;
            startIndex = Math.Max(copyNumber + 1, 2);
        }

        var copyName = string.Empty;

        if (startIndex == 1)
        {
            copyName = $"{baseName} (Copy)";
            if (nameSet.Contains(copyName))
                startIndex = 2;
        }

        if (startIndex > 1)
        {
            for (var i = startIndex; ; i++)
            {
                copyName = $"{baseName} (Copy {i})";
                if (!nameSet.Contains(copyName))
                    break;
            }
        }

        var newWorkflow = new Workflow
        {
            WorkflowId = Guid.NewGuid(),
            Version = workflow.Version,
            Named = copyName,
            Description = workflow.Description,
            IsConversationEnabled = workflow.IsConversationEnabled,
            Nodes = workflow.Nodes,
            Connections = workflow.Connections,
        };

        dbContext.Workflows.Add(newWorkflow);
        await dbContext.SaveChangesAsync();

        return newWorkflow.WorkflowId;
    }

    private static bool TryParseCopySuffix(string name, out string baseName, out int copyNumber)
    {
        baseName = name;
        copyNumber = 0;

        const string copySuffix = " (Copy)";
        if (name.EndsWith(copySuffix, StringComparison.OrdinalIgnoreCase))
        {
            baseName = name.Substring(0, name.Length - copySuffix.Length);
            copyNumber = 1;
            return true;
        }

        const string copyPrefix = " (Copy ";
        if (!name.EndsWith(")", StringComparison.OrdinalIgnoreCase))
            return false;

        var prefixIndex = name.LastIndexOf(copyPrefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0)
            return false;

        var numberStart = prefixIndex + copyPrefix.Length;
        var numberLength = name.Length - numberStart - 1;
        if (numberLength <= 0)
            return false;

        var numberSpan = name.AsSpan(numberStart, numberLength);
        if (!int.TryParse(numberSpan, out var parsed) || parsed < 1)
            return false;

        baseName = name.Substring(0, prefixIndex);
        copyNumber = parsed;
        return true;
    }

    private static IQueryable<Workflow> ApplyWorkflowSearch(IQueryable<Workflow> workflows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return workflows;

        var normalizedSearch = search.Trim().ToLower();
        return workflows.Where(workflow => workflow.Named.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<Workflow> GetSortedWorkflows(IQueryable<Workflow> workflows, WorkflowSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            WorkflowSortField.Description => sortDirection == SortDirection.Ascending
                ? workflows.OrderBy(workflow => workflow.Description).ThenBy(workflow => workflow.Named)
                : workflows.OrderByDescending(workflow => workflow.Description).ThenByDescending(workflow => workflow.Named),
            _ => sortDirection == SortDirection.Ascending
                ? workflows.OrderBy(workflow => workflow.Named).ThenBy(workflow => workflow.Description)
                : workflows.OrderByDescending(workflow => workflow.Named).ThenByDescending(workflow => workflow.Description),
        };
    }

    // ------------------------------------------------
    // Conversation Operations
    // ------------------------------------------------
    public async Task<Conversation?> GetLatestConversationForWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.Conversations.AsNoTracking().Where(c => c.WorkflowId == workflowId).OrderByDescending(c => c.Updated).ThenByDescending(c => c.Created).FirstOrDefaultAsync();
    }

    public async Task<Conversation?> GetConversation(string conversationId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.ConversationId == conversationId);
    }

    public async Task<int> GetWorkflowConversationCount(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.Conversations.AsNoTracking().Where(c => c.WorkflowId == workflowId).CountAsync();
    }

    public async Task<List<Conversation>> GetWorkflowConversations(Guid workflowId, ConversationSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var conversations = dbContext.Conversations.AsNoTracking().Where(c => c.WorkflowId == workflowId);
        var sortedConversations = GetSortedConversations(conversations, sortBy, sortDirection);

        if (skip > 0)
            sortedConversations = sortedConversations.Skip(skip);

        if (take > 0)
            sortedConversations = sortedConversations.Take(take);

        return await sortedConversations.ToListAsync();
    }

    public async Task UpsertConversation(Conversation conversation)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await dbContext.Conversations.FirstOrDefaultAsync(c => c.ConversationId == conversation.ConversationId);
        if (entity is null)
            dbContext.Conversations.Add(conversation);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(conversation);

        await dbContext.SaveChangesAsync();
    }

    public async Task<ConversationCheckpoint?> GetConversationCheckpoint(string conversationId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.ConversationCheckpoints.AsNoTracking().FirstOrDefaultAsync(c => c.ConversationId == conversationId);
    }

    public async Task UpsertConversationCheckpoint(ConversationCheckpoint checkpoint)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await dbContext.ConversationCheckpoints.FirstOrDefaultAsync(c => c.ConversationId == checkpoint.ConversationId);
        if (entity is null)
            dbContext.ConversationCheckpoints.Add(checkpoint);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(checkpoint);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteConversationCheckpoint(string conversationId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var entity = await dbContext.ConversationCheckpoints.FirstOrDefaultAsync(c => c.ConversationId == conversationId);
        if (entity is null)
            return;

        dbContext.ConversationCheckpoints.Remove(entity);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<Run>> GetConversationRuns(string conversationId, int skip = 0, int take = 0)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        IQueryable<Run> query = dbContext.Runs.AsNoTracking().Where(r => r.ConversationId == conversationId).OrderBy(r => r.TurnNumber).ThenBy(r => r.Created);
        if (skip > 0)
            query = query.Skip(skip);
        if (take > 0)
            query = query.Take(take);

        return await query.ToListAsync();
    }

    public async Task PruneWorkflowConversations(Guid workflowId, int keepLatest)
    {
        if (keepLatest < 0)
            keepLatest = 0;

        using var dbContext = dbContextFactory.CreateDbContext();

        var conversationsToDelete = await dbContext
            .Conversations
            .Where(c => c.WorkflowId == workflowId && c.Status != ConversationStatus.Suspended && c.Status != ConversationStatus.Running)
            .OrderByDescending(c => c.Updated)
            .Skip(keepLatest)
            .Select(c => c.ConversationId)
            .ToListAsync();

        if (conversationsToDelete.Count == 0)
            return;

        await DeleteAssetStorageForConversations(conversationsToDelete);

        await dbContext.Conversations.Where(c => conversationsToDelete.Contains(c.ConversationId)).ExecuteDeleteAsync();
    }

    private static IQueryable<Conversation> GetSortedConversations(IQueryable<Conversation> conversations, ConversationSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            ConversationSortField.Created => sortDirection == SortDirection.Ascending
                ? conversations.OrderBy(c => c.Created).ThenByDescending(c => c.Updated)
                : conversations.OrderByDescending(c => c.Created).ThenByDescending(c => c.Updated),
            ConversationSortField.Status => sortDirection == SortDirection.Ascending
                ? conversations.OrderBy(c => c.Status).ThenByDescending(c => c.Updated)
                : conversations.OrderByDescending(c => c.Status).ThenByDescending(c => c.Updated),
            _ => sortDirection == SortDirection.Ascending
                ? conversations.OrderBy(c => c.Updated).ThenByDescending(c => c.Created)
                : conversations.OrderByDescending(c => c.Updated).ThenByDescending(c => c.Created),
        };
    }

    // ------------------------------------------------
    // Run Operations
    // ------------------------------------------------
    public async Task<Run?> GetRun(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.RunId == runId);
    }

    public async Task<Run?> GetLatestRunForWorkflow(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.Runs.AsNoTracking().Where(r => r.WorkflowId == workflowId).OrderByDescending(r => r.Created).FirstOrDefaultAsync();
    }

    public async Task<int> GetWorkflowRunCount(Guid workflowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.Runs.AsNoTracking().Where(r => r.WorkflowId == workflowId).CountAsync();
    }

    public async Task<List<Run>> GetWorkflowRuns(Guid workflowId, RunSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var runs = dbContext.Runs.AsNoTracking().Where(r => r.WorkflowId == workflowId);

        var sortedRuns = GetSortedRuns(runs, sortBy, sortDirection);

        if (skip > 0)
            sortedRuns = sortedRuns.Skip(skip);

        if (take > 0)
            sortedRuns = sortedRuns.Take(take);

        return await sortedRuns.ToListAsync();
    }

    public async Task UpsertRun(Run run)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from r in dbContext.Runs where r.RunId == run.RunId select r).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.Runs.Add(run);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(run);

        await dbContext.SaveChangesAsync();
    }

    public async Task PruneWorkflowRuns(Guid workflowId, int keepLatest)
    {
        if (keepLatest < 0)
            keepLatest = 0;

        using var dbContext = dbContextFactory.CreateDbContext();

        var runIdsToDelete = await dbContext.Runs.Where(r => r.WorkflowId == workflowId && r.ConversationId == null).OrderByDescending(r => r.Created).Skip(keepLatest).Select(r => r.RunId).ToListAsync();

        if (runIdsToDelete.Count == 0)
            return;

        await DeleteAssetStorageForRuns(runIdsToDelete);

        await dbContext.Runs.Where(r => runIdsToDelete.Contains(r.RunId)).ExecuteDeleteAsync();
    }

    private static IQueryable<Run> GetSortedRuns(IQueryable<Run> runs, RunSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            RunSortField.Status => sortDirection == SortDirection.Ascending
                ? runs.OrderBy(r => r.RunStatus).ThenByDescending(r => r.Created)
                : runs.OrderByDescending(r => r.RunStatus).ThenByDescending(r => r.Created),
            _ => sortDirection == SortDirection.Ascending ? runs.OrderBy(r => r.Created) : runs.OrderByDescending(r => r.Created),
        };
    }

    // ------------------------------------------------
    // Trace Operations
    // ------------------------------------------------
    public async Task<List<Trace>> GetRunTraces(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from t in dbContext.Traces.AsNoTracking() where t.RunId == runId orderby t.Created select t).ToListAsync();
    }

    public async Task UpsertTrace(Trace trace)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from t in dbContext.Traces where t.TraceId == trace.TraceId select t).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.Traces.Add(trace);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(trace);

        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Infomration Operations
    // ------------------------------------------------
    public async Task<List<Information>> GetRunInformations(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from i in dbContext.Informations.AsNoTracking() where i.RunId == runId orderby i.Created select i).ToListAsync();
    }

    public async Task UpsertInformations(List<Information> informations)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (informations.Count == 0)
            return;

        var ids = informations.Select(information => information.InformationId).Distinct().ToList();
        var entities = await (from information in dbContext.Informations where ids.Contains(information.InformationId) select information).ToListAsync();

        foreach (var information in informations)
        {
            var entity = entities.Where(e => e.InformationId == information.InformationId).FirstOrDefault();
            if (entity is null)
                dbContext.Informations.Add(information);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(information);
        }

        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Stream Event Operations
    // ------------------------------------------------
    public async Task<int> GetNextStreamSequence(Guid runId, string? conversationId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var maxSequence = !string.IsNullOrWhiteSpace(conversationId)
            ? await dbContext.StreamEvents.AsNoTracking().Where(e => e.ConversationId == conversationId).MaxAsync(e => (int?)e.SequenceNumber)
            : await dbContext.StreamEvents.AsNoTracking().Where(e => e.RunId == runId).MaxAsync(e => (int?)e.SequenceNumber);

        return (maxSequence ?? 0) + 1;
    }

    public async Task AppendStreamEvents(List<StreamEvent> events)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (events.Count == 0)
            return;

        dbContext.StreamEvents.AddRange(events);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateStreamEventsHideFromReply(List<Guid> streamEventIds, bool hideFromReply)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (streamEventIds.Count == 0)
            return;

        await dbContext.StreamEvents.Where(e => streamEventIds.Contains(e.StreamEventId)).ExecuteUpdateAsync(setters => setters.SetProperty(e => e.HideFromReply, hideFromReply));
    }

    public async Task<List<StreamEvent>> GetRunStreamEvents(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.StreamEvents.AsNoTracking().Where(e => e.RunId == runId && !e.HideFromReply).OrderBy(e => e.SequenceNumber).ThenBy(e => e.Created).ToListAsync();
    }

    public async Task<List<StreamEvent>> GetConversationStreamEvents(string conversationId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (string.IsNullOrWhiteSpace(conversationId))
            throw new SharpOMaticException("Conversation stream id cannot be empty.");

        return await dbContext.StreamEvents.AsNoTracking().Where(e => e.ConversationId == conversationId && !e.HideFromReply).OrderBy(e => e.SequenceNumber).ThenBy(e => e.Created).ToListAsync();
    }

    public async Task<List<StreamEvent>> GetConversationStreamEventTail(string conversationId, int? beforeSequenceNumber, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (string.IsNullOrWhiteSpace(conversationId))
            throw new SharpOMaticException("Conversation stream id cannot be empty.");

        if (take <= 0)
            return [];

        var query = dbContext.StreamEvents.AsNoTracking().Where(e => e.ConversationId == conversationId && !e.HideFromReply);
        if (beforeSequenceNumber.HasValue)
            query = query.Where(e => e.SequenceNumber < beforeSequenceNumber.Value);

        return await query.OrderByDescending(e => e.SequenceNumber).ThenByDescending(e => e.Created).Take(take).ToListAsync();
    }

    public async Task<List<StreamEvent>> GetConversationStreamEventsByMessageIds(string conversationId, List<string> messageIds)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (string.IsNullOrWhiteSpace(conversationId))
            throw new SharpOMaticException("Conversation stream id cannot be empty.");

        var ids = messageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await dbContext.StreamEvents
            .AsNoTracking()
            .Where(e => e.ConversationId == conversationId && !e.HideFromReply && e.MessageId != null && ids.Contains(e.MessageId))
            .OrderBy(e => e.SequenceNumber)
            .ThenBy(e => e.Created)
            .ToListAsync();
    }

    public async Task<List<StreamEvent>> GetConversationStreamEventsByToolCallIds(string conversationId, List<string> toolCallIds)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (string.IsNullOrWhiteSpace(conversationId))
            throw new SharpOMaticException("Conversation stream id cannot be empty.");

        var ids = toolCallIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await dbContext.StreamEvents
            .AsNoTracking()
            .Where(e => e.ConversationId == conversationId && !e.HideFromReply && e.ToolCallId != null && ids.Contains(e.ToolCallId))
            .OrderBy(e => e.SequenceNumber)
            .ThenBy(e => e.Created)
            .ToListAsync();
    }

    public async Task<List<StreamEvent>> GetConversationStateStreamEvents(string conversationId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (string.IsNullOrWhiteSpace(conversationId))
            throw new SharpOMaticException("Conversation stream id cannot be empty.");

        return await dbContext.StreamEvents
            .AsNoTracking()
            .Where(e => e.ConversationId == conversationId && !e.HideFromReply && (e.EventKind == StreamEventKind.StateSnapshot || e.EventKind == StreamEventKind.StateDelta))
            .OrderBy(e => e.SequenceNumber)
            .ThenBy(e => e.Created)
            .ToListAsync();
    }

    // ------------------------------------------------
    // Model Call Metric Operations
    // ------------------------------------------------
    public async Task AppendModelCallMetric(ModelCallMetric metric)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        dbContext.ModelCallMetrics.Add(metric);
        await dbContext.SaveChangesAsync();
    }

    public async Task<ModelCallMetricsDashboard> GetModelCallMetricsDashboard(ModelCallMetricsDashboardRequest request)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var start = EnsureUtc(request.Start);
        var end = EnsureUtc(request.End);
        if (end <= start)
            end = start.AddDays(1);

        var recentSkip = Math.Max(0, request.RecentSkip);
        var recentTake = request.RecentTake <= 0 ? 25 : Math.Min(request.RecentTake, 100);

        var metricsInRange = await dbContext.ModelCallMetrics
            .AsNoTracking()
            .Where(metric => metric.Created >= start && metric.Created < end)
            .ToListAsync();

        var masterItems = request.Scope == ModelCallMetricScope.All
            ? []
            : BuildMetricMasterItems(metricsInRange, request.Scope, request.MasterSearch);

        var selectedMetrics = ApplyMetricScope(metricsInRange, request.Scope, request.ScopeKey).ToList();
        var scopeName = request.Scope == ModelCallMetricScope.All
            ? null
            : masterItems.FirstOrDefault(item => string.Equals(item.Key, request.ScopeKey, StringComparison.Ordinal))?.Name;

        return new ModelCallMetricsDashboard(
            start,
            end,
            request.Bucket,
            request.Scope,
            request.ScopeKey,
            scopeName,
            BuildMetricTotals(selectedMetrics),
            masterItems,
            BuildMetricTimeBuckets(selectedMetrics, start, end, request.Bucket),
            BuildMetricBreakdown(selectedMetrics, metric => BuildMetricIdKey(metric.WorkflowId), metric => metric.WorkflowName),
            BuildMetricBreakdown(selectedMetrics, metric => BuildMetricKey(metric.ConnectorId, metric.ConnectorName), GetMetricConnectorName),
            BuildMetricBreakdown(selectedMetrics, metric => BuildMetricKey(metric.ModelId, GetMetricModelName(metric)), GetMetricModelName),
            BuildMetricBreakdown(selectedMetrics, metric => BuildMetricIdKey(metric.NodeEntityId), metric => metric.NodeTitle),
            BuildMetricFailures(selectedMetrics),
            selectedMetrics
                .OrderByDescending(metric => metric.Created)
                .Skip(recentSkip)
                .Take(recentTake)
                .Select(ToMetricCallSummary)
                .ToList(),
            selectedMetrics.Count,
            selectedMetrics
                .OrderByDescending(metric => metric.Duration ?? -1)
                .ThenByDescending(metric => metric.Created)
                .Take(10)
                .Select(ToMetricCallSummary)
                .ToList()
        );
    }

    private static List<ModelCallMetricMasterItem> BuildMetricMasterItems(List<ModelCallMetric> metrics, ModelCallMetricScope scope, string? search)
    {
        var groups = scope switch
        {
            ModelCallMetricScope.Workflow => metrics.GroupBy(metric => new MetricGroupKey(BuildMetricIdKey(metric.WorkflowId), metric.WorkflowName)),
            ModelCallMetricScope.Connector => metrics.GroupBy(metric => new MetricGroupKey(BuildMetricKey(metric.ConnectorId, metric.ConnectorName), GetMetricConnectorName(metric))),
            ModelCallMetricScope.Model => metrics.GroupBy(metric => new MetricGroupKey(BuildMetricKey(metric.ModelId, GetMetricModelName(metric)), GetMetricModelName(metric))),
            _ => [],
        };

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var items = groups
            .Where(group => normalizedSearch is null || group.Key.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .Select(group =>
            {
                var groupMetrics = group.ToList();
                var totals = BuildMetricTotals(groupMetrics);
                return new ModelCallMetricMasterItem(
                    group.Key.Key,
                    group.Key.Name,
                    totals.TotalCalls,
                    totals.FailedCalls,
                    totals.TotalCost,
                    totals.TotalTokens,
                    totals.AverageDuration,
                    totals.P95Duration,
                    totals.FailureRate
                );
            });

        items = scope == ModelCallMetricScope.Connector
            ? items.OrderByDescending(item => item.TotalCalls).ThenBy(item => item.Name)
            : items.OrderByDescending(item => item.TotalCost).ThenByDescending(item => item.TotalCalls).ThenBy(item => item.Name);

        return items.ToList();
    }

    private static IEnumerable<ModelCallMetric> ApplyMetricScope(List<ModelCallMetric> metrics, ModelCallMetricScope scope, string? scopeKey)
    {
        if (scope == ModelCallMetricScope.All || string.IsNullOrWhiteSpace(scopeKey))
            return metrics;

        return scope switch
        {
            ModelCallMetricScope.Workflow => metrics.Where(metric => MetricIdKeyMatches(scopeKey, metric.WorkflowId)),
            ModelCallMetricScope.Connector => metrics.Where(metric => MetricKeyMatches(scopeKey, metric.ConnectorId, metric.ConnectorName)),
            ModelCallMetricScope.Model => metrics.Where(metric => MetricKeyMatches(scopeKey, metric.ModelId, GetMetricModelName(metric))),
            _ => metrics,
        };
    }

    private static ModelCallMetricTotals BuildMetricTotals(List<ModelCallMetric> metrics)
    {
        var totalCalls = metrics.Count;
        var failedCalls = metrics.Count(metric => !metric.Succeeded);
        return new ModelCallMetricTotals(
            totalCalls,
            totalCalls - failedCalls,
            failedCalls,
            metrics.Sum(metric => metric.InputTokens ?? 0),
            metrics.Sum(metric => metric.OutputTokens ?? 0),
            metrics.Sum(metric => metric.TotalTokens ?? 0),
            metrics.Sum(metric => metric.TotalCost ?? 0),
            metrics.Count(metric => metric.TotalCost.HasValue),
            metrics.Count(metric => !metric.TotalCost.HasValue),
            AverageDuration(metrics),
            PercentileDuration(metrics, 0.95),
            totalCalls == 0 ? 0 : failedCalls / (double)totalCalls
        );
    }

    private static List<ModelCallMetricTimeBucket> BuildMetricTimeBuckets(List<ModelCallMetric> metrics, DateTime start, DateTime end, ModelCallMetricBucket bucket)
    {
        var groups = metrics
            .GroupBy(metric => GetMetricBucketStart(metric.Created, bucket))
            .ToDictionary(group => group.Key, group => group.ToList());

        var results = new List<ModelCallMetricTimeBucket>();
        for (var bucketStart = GetMetricBucketStart(start, bucket); bucketStart < end; bucketStart = AddMetricBucket(bucketStart, bucket))
        {
            groups.TryGetValue(bucketStart, out var bucketMetrics);
            bucketMetrics ??= [];
            var totals = BuildMetricTotals(bucketMetrics);
            results.Add(
                new ModelCallMetricTimeBucket(
                    bucketStart,
                    totals.TotalCalls,
                    totals.SuccessfulCalls,
                    totals.FailedCalls,
                    totals.InputTokens,
                    totals.OutputTokens,
                    totals.TotalTokens,
                    totals.TotalCost,
                    totals.PricedCalls,
                    totals.UnpricedCalls,
                    totals.AverageDuration,
                    totals.P95Duration
                )
            );
        }

        return results;
    }

    private static List<ModelCallMetricBreakdownItem> BuildMetricBreakdown(List<ModelCallMetric> metrics, Func<ModelCallMetric, string> keySelector, Func<ModelCallMetric, string> nameSelector)
    {
        return metrics
            .GroupBy(metric => new MetricGroupKey(keySelector(metric), nameSelector(metric)))
            .Select(group =>
            {
                var groupMetrics = group.ToList();
                var totals = BuildMetricTotals(groupMetrics);
                return new ModelCallMetricBreakdownItem(
                    group.Key.Key,
                    group.Key.Name,
                    totals.TotalCalls,
                    totals.FailedCalls,
                    totals.TotalCost,
                    totals.TotalTokens,
                    totals.AverageDuration,
                    totals.P95Duration,
                    totals.FailureRate
                );
            })
            .OrderByDescending(item => item.TotalCost)
            .ThenByDescending(item => item.TotalCalls)
            .ThenBy(item => item.Name)
            .Take(8)
            .ToList();
    }

    private static List<ModelCallMetricFailureGroup> BuildMetricFailures(List<ModelCallMetric> metrics)
    {
        return metrics
            .Where(metric => !metric.Succeeded)
            .GroupBy(metric => new
            {
                ErrorType = string.IsNullOrWhiteSpace(metric.ErrorType) ? "Unknown" : metric.ErrorType,
                ErrorMessage = string.IsNullOrWhiteSpace(metric.ErrorMessage) ? "Unknown failure" : metric.ErrorMessage,
            })
            .Select(group => new ModelCallMetricFailureGroup(
                group.Key.ErrorType,
                group.Key.ErrorMessage,
                group.Count(),
                group.Min(metric => metric.Created),
                group.Max(metric => metric.Created),
                group.Select(metric => metric.WorkflowId).Distinct().Count(),
                group.Select(metric => BuildMetricKey(metric.ConnectorId, metric.ConnectorName)).Distinct(StringComparer.Ordinal).Count(),
                group.Select(metric => BuildMetricKey(metric.ModelId, GetMetricModelName(metric))).Distinct(StringComparer.Ordinal).Count()
            ))
            .OrderByDescending(group => group.Count)
            .ThenByDescending(group => group.LastSeen)
            .Take(10)
            .ToList();
    }

    private static ModelCallMetricCallSummary ToMetricCallSummary(ModelCallMetric metric)
    {
        return new ModelCallMetricCallSummary(
            metric.Id,
            metric.Created,
            metric.WorkflowName,
            metric.NodeTitle,
            metric.ConnectorName,
            GetMetricModelName(metric),
            metric.Duration,
            metric.InputTokens,
            metric.OutputTokens,
            metric.TotalTokens,
            metric.TotalCost,
            metric.Succeeded,
            metric.ErrorType,
            metric.ErrorMessage
        );
    }

    private static double? AverageDuration(List<ModelCallMetric> metrics)
    {
        var durations = metrics.Where(metric => metric.Duration.HasValue).Select(metric => metric.Duration!.Value).ToList();
        return durations.Count == 0 ? null : durations.Average();
    }

    private static long? PercentileDuration(List<ModelCallMetric> metrics, double percentile)
    {
        var durations = metrics.Where(metric => metric.Duration.HasValue).Select(metric => metric.Duration!.Value).OrderBy(duration => duration).ToList();
        if (durations.Count == 0)
            return null;

        var index = (int)Math.Ceiling(durations.Count * percentile) - 1;
        index = Math.Clamp(index, 0, durations.Count - 1);
        return durations[index];
    }

    private static string BuildMetricIdKey(Guid id) => $"id:{id:D}";

    private static string BuildMetricKey(Guid? id, string? name)
    {
        return id.HasValue ? BuildMetricIdKey(id.Value) : $"name:{GetMetricDisplayName(name, "Unknown")}";
    }

    private static bool MetricIdKeyMatches(string key, Guid id)
    {
        return string.Equals(key, BuildMetricIdKey(id), StringComparison.Ordinal);
    }

    private static bool MetricKeyMatches(string key, Guid? id, string? name)
    {
        return id.HasValue
            ? MetricIdKeyMatches(key, id.Value)
            : string.Equals(key, $"name:{GetMetricDisplayName(name, "Unknown")}", StringComparison.Ordinal);
    }

    private static string GetMetricConnectorName(ModelCallMetric metric) => GetMetricDisplayName(metric.ConnectorName, "No connector");

    private static string GetMetricModelName(ModelCallMetric metric) => GetMetricDisplayName(metric.ModelName ?? metric.ProviderModelName, "No model");

    private static string GetMetricDisplayName(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static DateTime GetMetricBucketStart(DateTime value, ModelCallMetricBucket bucket)
    {
        value = EnsureUtc(value);
        return bucket switch
        {
            ModelCallMetricBucket.Hour => new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc),
            ModelCallMetricBucket.Week => value.Date.AddDays(-GetDaysSinceMonday(value)),
            _ => value.Date,
        };
    }

    private static DateTime AddMetricBucket(DateTime value, ModelCallMetricBucket bucket)
    {
        return bucket switch
        {
            ModelCallMetricBucket.Hour => value.AddHours(1),
            ModelCallMetricBucket.Week => value.AddDays(7),
            _ => value.AddDays(1),
        };
    }

    private static int GetDaysSinceMonday(DateTime value)
    {
        return ((int)value.DayOfWeek + 6) % 7;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    private readonly record struct MetricGroupKey(string Key, string Name);

    // ------------------------------------------------
    // ConnectorConfig Operations
    // ------------------------------------------------
    public async Task<ConnectorConfig?> GetConnectorConfig(string configId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorConfigMetadata where c.ConfigId == configId select c)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (metadata is null)
            return null;

        return JsonSerializer.Deserialize<ConnectorConfig>(metadata.Config, _options);
    }

    public async Task<List<ConnectorConfig>> GetConnectorConfigs()
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entries = await dbContext.ConnectorConfigMetadata.AsNoTracking().ToListAsync();

        var results = new List<ConnectorConfig>();
        foreach (var entry in entries)
        {
            var config = JsonSerializer.Deserialize<ConnectorConfig>(entry.Config, _options);
            if (config != null)
                results.Add(config);
        }

        return results;
    }

    public async Task UpsertConnectorConfig(ConnectorConfig config)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorConfigMetadata where c.ConfigId == config.ConfigId select c).FirstOrDefaultAsync();

        if (metadata is null)
        {
            metadata = new ConnectorConfigMetadata()
            {
                Version = config.Version,
                ConfigId = config.ConfigId,
                Config = "",
            };

            dbContext.ConnectorConfigMetadata.Add(metadata);
        }

        metadata.Config = JsonSerializer.Serialize(config, _options);
        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Connector Operations
    // ------------------------------------------------
    public async Task<List<ConnectorSummary>> GetConnectorSummaries()
    {
        return await GetConnectorSummaries(null, ConnectorSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetConnectorSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var connectors = ApplyConnectorSearch(dbContext.ConnectorMetadata.AsNoTracking(), search);
        return await connectors.CountAsync();
    }

    public async Task<List<ConnectorSummary>> GetConnectorSummaries(string? search, ConnectorSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var connectors = ApplyConnectorSearch(dbContext.ConnectorMetadata.AsNoTracking(), search);
        var sorted = GetSortedConnectors(connectors, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(connector => new ConnectorSummary
            {
                ConnectorId = connector.ConnectorId,
                Name = connector.Name,
                Description = connector.Description,
            })
            .ToListAsync();
    }

    public async Task<Connector> GetConnector(Guid connectorId, bool hideSecrets = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorMetadata where c.ConnectorId == connectorId select c)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Connector '{connectorId}' cannot be found.");

        var connector = JsonSerializer.Deserialize<Connector>(metadata.Config);

        if (connector is null)
            throw new SharpOMaticException($"Connector '{connectorId}' configuration is invalid.");

        // We need to ensure that any field that is a secret, is replaced to prevent it being available to clients
        if (hideSecrets && (connector.FieldValues.Count > 0) && !string.IsNullOrWhiteSpace(connector.ConfigId))
        {
            var config = await GetConnectorConfig(connector.ConfigId);
            if (config is not null)
            {
                foreach (var authModes in config.AuthModes)
                {
                    foreach (var field in authModes.Fields)
                        if ((field.Type == FieldDescriptorType.Secret) && connector.FieldValues.ContainsKey(field.Name))
                            connector.FieldValues[field.Name] = SECRET_OBFUSCATION;
                }
            }
        }

        return connector;
    }

    public async Task UpsertConnector(Connector connector, bool hideSecrets = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entry = await (from c in dbContext.ConnectorMetadata where c.ConnectorId == connector.ConnectorId select c).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new ConnectorMetadata()
            {
                ConnectorId = connector.ConnectorId,
                Version = connector.Version,
                Name = "",
                Description = "",
                Config = "",
            };

            dbContext.ConnectorMetadata.Add(entry);
        }
        else if (hideSecrets)
        {
            // If any provided secrets are the obfuscated value then we do not want to overwrite the existing value
            var entryConfig = JsonSerializer.Deserialize<Connector>(entry.Config);
            if (entryConfig is not null)
            {
                var config = await GetConnectorConfig(connector.ConfigId);
                if (config is not null)
                {
                    foreach (var authModes in config.AuthModes)
                    {
                        foreach (var field in authModes.Fields)
                        {
                            if (
                                (field.Type == FieldDescriptorType.Secret)
                                && connector.FieldValues.ContainsKey(field.Name)
                                && entryConfig.FieldValues.ContainsKey(field.Name)
                                && (connector.FieldValues[field.Name] == SECRET_OBFUSCATION)
                            )
                            {
                                connector.FieldValues[field.Name] = entryConfig.FieldValues[field.Name];
                            }
                        }
                    }
                }
            }
        }

        entry.Name = connector.Name;
        entry.Description = connector.Description;
        entry.Config = JsonSerializer.Serialize(connector);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteConnector(Guid connectorId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from c in dbContext.ConnectorMetadata where c.ConnectorId == connectorId select c).FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Connector '{connectorId}' cannot be found.");

        dbContext.Remove(metadata);
        await dbContext.SaveChangesAsync();
    }

    private static IQueryable<ConnectorMetadata> ApplyConnectorSearch(IQueryable<ConnectorMetadata> connectors, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return connectors;

        var normalizedSearch = search.Trim().ToLower();
        return connectors.Where(connector => connector.Name.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<ConnectorMetadata> GetSortedConnectors(IQueryable<ConnectorMetadata> connectors, ConnectorSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            ConnectorSortField.Description => sortDirection == SortDirection.Ascending
                ? connectors.OrderBy(connector => connector.Description).ThenBy(connector => connector.Name)
                : connectors.OrderByDescending(connector => connector.Description).ThenByDescending(connector => connector.Name),
            _ => sortDirection == SortDirection.Ascending
                ? connectors.OrderBy(connector => connector.Name).ThenBy(connector => connector.Description)
                : connectors.OrderByDescending(connector => connector.Name).ThenByDescending(connector => connector.Description),
        };
    }

    // ------------------------------------------------
    // ModelConfig Operations
    // ------------------------------------------------
    public async Task<ModelConfig?> GetModelConfig(string configId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelConfigMetadata where m.ConfigId == configId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (metadata is null)
            return null;

        return JsonSerializer.Deserialize<ModelConfig>(metadata.Config, _options);
    }

    public async Task<List<ModelConfig>> GetModelConfigs()
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entries = await dbContext.ModelConfigMetadata.AsNoTracking().ToListAsync();

        var results = new List<ModelConfig>();
        foreach (var entry in entries)
        {
            var config = JsonSerializer.Deserialize<ModelConfig>(entry.Config, _options);
            if (config != null)
                results.Add(config);
        }

        return results;
    }

    public async Task UpsertModelConfig(ModelConfig config)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelConfigMetadata where m.ConfigId == config.ConfigId select m).FirstOrDefaultAsync();

        if (metadata is null)
        {
            metadata = new ModelConfigMetadata()
            {
                Version = config.Version,
                ConfigId = config.ConfigId,
                Config = "",
            };

            dbContext.ModelConfigMetadata.Add(metadata);
        }

        metadata.Config = JsonSerializer.Serialize(config, _options);
        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Model Operations
    // ------------------------------------------------
    public async Task<List<ModelSummary>> GetModelSummaries()
    {
        return await GetModelSummaries(null, ModelSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetModelSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var models = ApplyModelSearch(dbContext.ModelMetadata.AsNoTracking(), search);
        return await models.CountAsync();
    }

    public async Task<List<ModelSummary>> GetModelSummaries(string? search, ModelSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var models = ApplyModelSearch(dbContext.ModelMetadata.AsNoTracking(), search);
        var sorted = GetSortedModels(models, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(model => new ModelSummary
            {
                ModelId = model.ModelId,
                Name = model.Name,
                Description = model.Description,
            })
            .ToListAsync();
    }

    public async Task<Model> GetModel(Guid modelId, bool hideSecrets = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelMetadata where m.ModelId == modelId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Model '{modelId}' cannot be found.");

        var model = JsonSerializer.Deserialize<Model>(metadata.Config);

        if (model is null)
            throw new SharpOMaticException($"Model '{modelId}' configuration is invalid.");

        // We need to ensure that any parameter that is a secret, is replaced to prevent it being available in the client
        if (hideSecrets && (model.ParameterValues.Count > 0) && !string.IsNullOrWhiteSpace(model.ConfigId))
        {
            var config = await GetModelConfig(model.ConfigId);
            if (config is not null)
            {
                foreach (var field in config.ParameterFields)
                    if ((field.Type == FieldDescriptorType.Secret) && model.ParameterValues.ContainsKey(field.Name))
                        model.ParameterValues[field.Name] = "**********";
            }
        }

        return model;
    }

    public async Task UpsertModel(Model model)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entry = await (from m in dbContext.ModelMetadata where m.ModelId == model.ModelId select m).FirstOrDefaultAsync();

        if (entry is null)
        {
            entry = new ModelMetadata()
            {
                ModelId = model.ModelId,
                Version = model.Version,
                Name = "",
                Description = "",
                Config = "",
            };

            dbContext.ModelMetadata.Add(entry);
        }

        entry.Name = model.Name;
        entry.Description = model.Description;
        entry.Config = JsonSerializer.Serialize(model);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteModel(Guid modelId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var metadata = await (from m in dbContext.ModelMetadata where m.ModelId == modelId select m).FirstOrDefaultAsync();

        if (metadata is null)
            throw new SharpOMaticException($"Model '{modelId}' cannot be found.");

        dbContext.Remove(metadata);
        await dbContext.SaveChangesAsync();
    }

    private static IQueryable<ModelMetadata> ApplyModelSearch(IQueryable<ModelMetadata> models, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return models;

        var normalizedSearch = search.Trim().ToLower();
        return models.Where(model => model.Name.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<ModelMetadata> GetSortedModels(IQueryable<ModelMetadata> models, ModelSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            ModelSortField.Description => sortDirection == SortDirection.Ascending
                ? models.OrderBy(model => model.Description).ThenBy(model => model.Name)
                : models.OrderByDescending(model => model.Description).ThenByDescending(model => model.Name),
            _ => sortDirection == SortDirection.Ascending
                ? models.OrderBy(model => model.Name).ThenBy(model => model.Description)
                : models.OrderByDescending(model => model.Name).ThenByDescending(model => model.Description),
        };
    }

    // ------------------------------------------------
    // EvalConfig Operations
    // ------------------------------------------------
    public async Task<List<EvalConfigSummary>> GetEvalConfigSummaries()
    {
        return await GetEvalConfigSummaries(null, EvalConfigSortField.Name, SortDirection.Ascending, 0, 0);
    }

    public async Task<int> GetEvalConfigSummaryCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var evalConfigs = ApplyModelSearch(dbContext.EvalConfigs.AsNoTracking(), search);
        return await evalConfigs.CountAsync();
    }

    public async Task<List<EvalConfigSummary>> GetEvalConfigSummaries(string? search, EvalConfigSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfigs = ApplyModelSearch(dbContext.EvalConfigs.AsNoTracking(), search);
        var sorted = GetSortedModels(evalConfigs, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(config => new EvalConfigSummary
            {
                EvalConfigId = config.EvalConfigId,
                Name = config.Name,
                Description = config.Description,
            })
            .ToListAsync();
    }

    public async Task<EvalConfig> GetEvalConfig(Guid evalConfigId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfig = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfigId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (evalConfig is null)
            throw new SharpOMaticException($"EvalConfig '{evalConfigId}' cannot be found.");

        return evalConfig;
    }

    public async Task<EvalConfigDetail> GetEvalConfigDetail(Guid evalConfigId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfig = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfigId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (evalConfig is null)
            throw new SharpOMaticException($"EvalConfig '{evalConfigId}' cannot be found.");

        var graders = await (from g in dbContext.EvalGraders.AsNoTracking() where g.EvalConfigId == evalConfigId orderby g.Order select g).ToListAsync();

        var columns = await (from c in dbContext.EvalColumns.AsNoTracking() where c.EvalConfigId == evalConfigId orderby c.Order select c).ToListAsync();

        var rows = await (from r in dbContext.EvalRows.AsNoTracking() where r.EvalConfigId == evalConfigId orderby r.Order select r).ToListAsync();

        var data = await (
            from d in dbContext.EvalData.AsNoTracking()
            join r in dbContext.EvalRows.AsNoTracking() on d.EvalRowId equals r.EvalRowId
            join c in dbContext.EvalColumns.AsNoTracking() on d.EvalColumnId equals c.EvalColumnId
            where r.EvalConfigId == evalConfigId && c.EvalConfigId == evalConfigId
            orderby r.Order, c.Order
            select d
        ).ToListAsync();

        return new EvalConfigDetail
        {
            EvalConfig = evalConfig,
            Graders = graders,
            Columns = columns,
            Rows = rows,
            Data = data,
        };
    }

    public async Task<TransferEvaluationPackage> GetEvalTransferPackage(Guid evalConfigId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfig = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfigId select m)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (evalConfig is null)
            throw new SharpOMaticException($"EvalConfig '{evalConfigId}' cannot be found.");

        var graders = await (from g in dbContext.EvalGraders.AsNoTracking() where g.EvalConfigId == evalConfigId orderby g.Order select g).ToListAsync();

        var columns = await (from c in dbContext.EvalColumns.AsNoTracking() where c.EvalConfigId == evalConfigId orderby c.Order select c).ToListAsync();

        var rows = await (from r in dbContext.EvalRows.AsNoTracking() where r.EvalConfigId == evalConfigId orderby r.Order select r).ToListAsync();

        var data = await (
            from d in dbContext.EvalData.AsNoTracking()
            join r in dbContext.EvalRows.AsNoTracking() on d.EvalRowId equals r.EvalRowId
            join c in dbContext.EvalColumns.AsNoTracking() on d.EvalColumnId equals c.EvalColumnId
            where r.EvalConfigId == evalConfigId && c.EvalConfigId == evalConfigId
            orderby r.Order, c.Order
            select d
        ).ToListAsync();

        var runs = await dbContext
            .EvalRuns
            .AsNoTracking()
            .Where(run => run.EvalConfigId == evalConfigId && run.Status != EvalRunStatus.Running)
            .OrderBy(run => run.Order)
            .ThenBy(run => run.Started)
            .ThenBy(run => run.EvalRunId)
            .ToListAsync();

        var runIds = runs.Select(run => run.EvalRunId).ToList();

        var runRows = runIds.Count == 0
            ? new List<EvalRunRow>()
            : await dbContext
                .EvalRunRows
                .AsNoTracking()
                .Where(row => runIds.Contains(row.EvalRunId))
                .OrderBy(row => row.EvalRunId)
                .ThenBy(row => row.Order)
                .ThenBy(row => row.EvalRunRowId)
                .ToListAsync();

        var runRowGraders = runIds.Count == 0
            ? new List<EvalRunRowGrader>()
            : await dbContext
                .EvalRunRowGraders
                .AsNoTracking()
                .Where(grader => runIds.Contains(grader.EvalRunId))
                .OrderBy(grader => grader.EvalRunId)
                .ThenBy(grader => grader.EvalRunRowId)
                .ThenBy(grader => grader.EvalGraderId)
                .ThenBy(grader => grader.EvalRunRowGraderId)
                .ToListAsync();

        var runGraderSummaries = runIds.Count == 0
            ? new List<EvalRunGraderSummary>()
            : await dbContext
                .EvalRunGraderSummaries
                .AsNoTracking()
                .Where(summary => runIds.Contains(summary.EvalRunId))
                .OrderBy(summary => summary.EvalRunId)
                .ThenBy(summary => summary.EvalGraderId)
                .ThenBy(summary => summary.EvalRunGraderSummaryId)
                .ToListAsync();

        return new TransferEvaluationPackage
        {
            EvalConfig = evalConfig,
            Graders = graders,
            Columns = columns,
            Rows = rows,
            Data = data,
            Runs = runs,
            RunRows = runRows,
            RunRowGraders = runRowGraders,
            RunGraderSummaries = runGraderSummaries,
        };
    }

    public async Task UpsertEvalConfig(EvalConfig evalConfig)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfig.EvalConfigId select m).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.EvalConfigs.Add(evalConfig);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(evalConfig);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalConfig(Guid evalConfigId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalConfig = await (from m in dbContext.EvalConfigs where m.EvalConfigId == evalConfigId select m).FirstOrDefaultAsync();

        if (evalConfig is null)
            throw new SharpOMaticException($"EvalConfig '{evalConfigId}' cannot be found.");

        dbContext.Remove(evalConfig);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalGraders(List<EvalGrader> graders)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (graders.Count == 0)
            return;

        var ids = graders.Select(grader => grader.EvalGraderId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalGraders where ids.Contains(run.EvalGraderId) select run).ToListAsync();

        foreach (var grader in graders)
        {
            var entity = entities.Where(e => e.EvalGraderId == grader.EvalGraderId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalGraders.Add(grader);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(grader);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalGrader(Guid evalGraderId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var grader = await (from g in dbContext.EvalGraders where g.EvalGraderId == evalGraderId select g).FirstOrDefaultAsync();

        if (grader is null)
            throw new SharpOMaticException($"EvalGrader '{evalGraderId}' cannot be found.");

        await (from g in dbContext.EvalRunRowGraders where g.EvalGraderId == evalGraderId select g).ExecuteDeleteAsync();
        await (from g in dbContext.EvalRunGraderSummaries where g.EvalGraderId == evalGraderId select g).ExecuteDeleteAsync();

        dbContext.Remove(grader);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalColumns(List<EvalColumn> columns)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (columns.Count == 0)
            return;

        var ids = columns.Select(row => row.EvalColumnId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalColumns where ids.Contains(run.EvalColumnId) select run).ToListAsync();

        foreach (var column in columns)
        {
            var entity = entities.Where(e => e.EvalColumnId == column.EvalColumnId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalColumns.Add(column);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(column);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalColumn(Guid evalColumnId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var column = await (from c in dbContext.EvalColumns where c.EvalColumnId == evalColumnId select c).FirstOrDefaultAsync();

        if (column is null)
            throw new SharpOMaticException($"EvalColumn '{evalColumnId}' cannot be found.");

        await (from d in dbContext.EvalData where d.EvalColumnId == evalColumnId select d).ExecuteDeleteAsync();

        dbContext.Remove(column);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalRows(List<EvalRow> rows)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (rows.Count == 0)
            return;

        var ids = rows.Select(row => row.EvalRowId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalRows where ids.Contains(run.EvalRowId) select run).ToListAsync();

        foreach (var row in rows)
        {
            var entity = entities.Where(e => e.EvalRowId == row.EvalRowId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalRows.Add(row);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(row);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalRow(Guid evalRowId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var row = await (from r in dbContext.EvalRows where r.EvalRowId == evalRowId select r).FirstOrDefaultAsync();

        if (row is null)
            throw new SharpOMaticException($"EvalRow '{evalRowId}' cannot be found.");

        await (from d in dbContext.EvalData where d.EvalRowId == evalRowId select d).ExecuteDeleteAsync();
        await (from r in dbContext.EvalRunRows where r.EvalRowId == evalRowId select r).ExecuteDeleteAsync();

        dbContext.Remove(row);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpsertEvalData(List<EvalData> data)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (data.Count == 0)
            return;

        var ids = data.Select(row => row.EvalDataId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalData where ids.Contains(run.EvalDataId) select run).ToListAsync();

        foreach (var row in data)
        {
            var entity = entities.Where(e => e.EvalDataId == row.EvalDataId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalData.Add(row);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(row);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteEvalData(Guid evalDataId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var data = await (from d in dbContext.EvalData where d.EvalDataId == evalDataId select d).FirstOrDefaultAsync();

        if (data is null)
            throw new SharpOMaticException($"EvalData '{evalDataId}' cannot be found.");

        dbContext.Remove(data);
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> UpsertEvalRun(EvalRun evalRun, bool allowInsert = true)
    {
        const int maxInsertAttempts = 2;

        for (var attempt = 1; attempt <= maxInsertAttempts; attempt++)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            var entity = await (from run in dbContext.EvalRuns where run.EvalRunId == evalRun.EvalRunId select run).FirstOrDefaultAsync();

            if (entity is null)
            {
                if (!allowInsert)
                    return false;

                evalRun.Order = evalRun.Order > 0 ? evalRun.Order : await GetNextEvalRunOrder(dbContext, evalRun.EvalConfigId);
                dbContext.EvalRuns.Add(evalRun);
            }
            else
            {
                evalRun.Order = entity.Order;
                dbContext.Entry(entity).CurrentValues.SetValues(evalRun);
                dbContext.Entry(entity).Property(run => run.CancelRequested).IsModified = false;
            }

            try
            {
                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (DbUpdateException ex) when (entity is null && attempt < maxInsertAttempts && IsUniqueEvalRunOrderViolation(ex))
            {
                await transaction.RollbackAsync();
            }
        }

        throw new SharpOMaticException($"EvalRun '{evalRun.EvalRunId}' could not be saved because a unique run order could not be allocated.");
    }

    public async Task<bool> UpsertEvalRunRows(List<EvalRunRow> runRows, bool allowInsert = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (runRows.Count == 0)
            return false;

        var ids = runRows.Select(row => row.EvalRunRowId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalRunRows where ids.Contains(run.EvalRunRowId) select run).ToListAsync();
        var missingRunIds = runRows.Where(row => entities.All(entity => entity.EvalRunRowId != row.EvalRunRowId)).Select(row => row.EvalRunId).Distinct().ToList();
        var existingRunIds = allowInsert && missingRunIds.Count > 0
            ? (await dbContext.EvalRuns.Where(run => missingRunIds.Contains(run.EvalRunId)).Select(run => run.EvalRunId).ToListAsync()).ToHashSet()
            : [];
        var hasChanges = false;

        foreach (var row in runRows)
        {
            var entity = entities.Where(e => e.EvalRunRowId == row.EvalRunRowId).FirstOrDefault();
            if (entity is null)
            {
                if (!allowInsert || !existingRunIds.Contains(row.EvalRunId))
                    continue;

                dbContext.EvalRunRows.Add(row);
            }
            else
            {
                dbContext.Entry(entity).CurrentValues.SetValues(row);
            }

            hasChanges = true;
        }

        if (!hasChanges)
            return false;

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpsertEvalRunRowGraders(List<EvalRunRowGrader> runRowGraders, bool allowInsert = true)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (runRowGraders.Count == 0)
            return false;

        var ids = runRowGraders.Select(row => row.EvalRunRowGraderId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalRunRowGraders where ids.Contains(run.EvalRunRowGraderId) select run).ToListAsync();
        var missingRowIds = runRowGraders.Where(row => entities.All(entity => entity.EvalRunRowGraderId != row.EvalRunRowGraderId)).Select(row => row.EvalRunRowId).Distinct().ToList();
        var existingRowIds = allowInsert && missingRowIds.Count > 0
            ? (await dbContext.EvalRunRows.Where(row => missingRowIds.Contains(row.EvalRunRowId)).Select(row => row.EvalRunRowId).ToListAsync()).ToHashSet()
            : [];
        var hasChanges = false;

        foreach (var row in runRowGraders)
        {
            var entity = entities.Where(e => e.EvalRunRowGraderId == row.EvalRunRowGraderId).FirstOrDefault();
            if (entity is null)
            {
                if (!allowInsert || !existingRowIds.Contains(row.EvalRunRowId))
                    continue;

                dbContext.EvalRunRowGraders.Add(row);
            }
            else
            {
                dbContext.Entry(entity).CurrentValues.SetValues(row);
            }

            hasChanges = true;
        }

        if (!hasChanges)
            return false;

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task UpsertEvalRunGraderSummaries(List<EvalRunGraderSummary> graderSummaries)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        if (graderSummaries.Count == 0)
            return;

        var ids = graderSummaries.Select(row => row.EvalRunGraderSummaryId).Distinct().ToList();
        var entities = await (from run in dbContext.EvalRunGraderSummaries where ids.Contains(run.EvalRunGraderSummaryId) select run).ToListAsync();

        foreach (var summary in graderSummaries)
        {
            var entity = entities.Where(e => e.EvalRunGraderSummaryId == summary.EvalRunGraderSummaryId).FirstOrDefault();
            if (entity is null)
                dbContext.EvalRunGraderSummaries.Add(summary);
            else
                dbContext.Entry(entity).CurrentValues.SetValues(summary);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<List<EvalRunRowGrader>> GetEvalRunRowGraders(Guid evalRunId, Guid evalGraderId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await (from run in dbContext.EvalRuns.AsNoTracking() where run.EvalRunId == evalRunId select run).FirstOrDefaultAsync();
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var evalGrader = await (from grader in dbContext.EvalGraders.AsNoTracking() where grader.EvalGraderId == evalGraderId select grader).FirstOrDefaultAsync();
        if (evalGrader is null || evalGrader.EvalConfigId != evalRun.EvalConfigId)
            throw new SharpOMaticException($"EvalGrader '{evalGraderId}' does not belong to EvalRun '{evalRunId}'.");

        return await (
            from runRowGrader in dbContext.EvalRunRowGraders.AsNoTracking()
            where runRowGrader.EvalRunId == evalRunId && runRowGrader.EvalGraderId == evalGraderId
            select runRowGrader
        ).ToListAsync();
    }

    public async Task<int> GetEvalRunSummaryCount(Guid evalConfigId, string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRuns = dbContext.EvalRuns.AsNoTracking().Where(run => run.EvalConfigId == evalConfigId);
        evalRuns = ApplyEvalRunSearch(evalRuns, search);
        return await evalRuns.CountAsync();
    }

    public async Task<List<EvalRunSummary>> GetEvalRunSummaries(Guid evalConfigId, string? search, EvalRunSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRuns = dbContext.EvalRuns.AsNoTracking().Where(run => run.EvalConfigId == evalConfigId);
        evalRuns = ApplyEvalRunSearch(evalRuns, search);
        var sorted = GetSortedEvalRuns(evalRuns, sortBy, sortDirection);

        if (skip > 0)
            sorted = sorted.Skip(skip);

        if (take > 0)
            sorted = sorted.Take(take);

        return await sorted
            .Select(run => new EvalRunSummary
            {
                EvalRunId = run.EvalRunId,
                EvalConfigId = run.EvalConfigId,
                Name = run.Name,
                Order = run.Order,
                Started = run.Started,
                Finished = run.Finished,
                Status = run.Status,
                Message = run.Message,
                Error = run.Error,
                TotalRows = run.TotalRows,
                CompletedRows = run.CompletedRows,
                FailedRows = run.FailedRows,
                AveragePassRate = run.AveragePassRate,
                RunScoreMode = run.RunScoreMode,
                Score = run.Score,
            })
            .ToListAsync();
    }

    public async Task<EvalRun> GetEvalRun(Guid evalRunId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.AsNoTracking().FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);

        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        return evalRun;
    }

    public async Task RequestCancelEvalRun(Guid evalRunId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        if (evalRun.Status != EvalRunStatus.Running)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' is not running and cannot be canceled.");

        if (evalRun.CancelRequested)
            return;

        evalRun.CancelRequested = true;
        await dbContext.SaveChangesAsync();
    }

    public async Task RenameEvalRun(Guid evalRunId, string name)
    {
        var normalizedName = name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new SharpOMaticException("EvalRun name cannot be empty.");

        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        evalRun.Name = normalizedName;
        await dbContext.SaveChangesAsync();
    }

    public async Task MoveEvalRun(Guid evalRunId, MoveDirection direction)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var evalRun = await dbContext.EvalRuns.FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var adjacentRun = direction switch
        {
            MoveDirection.Up => await dbContext
                .EvalRuns
                .Where(run => run.EvalConfigId == evalRun.EvalConfigId && run.Order > evalRun.Order)
                .OrderBy(run => run.Order)
                .ThenByDescending(run => run.Started)
                .ThenBy(run => run.EvalRunId)
                .FirstOrDefaultAsync(),
            MoveDirection.Down => await dbContext
                .EvalRuns
                .Where(run => run.EvalConfigId == evalRun.EvalConfigId && run.Order < evalRun.Order)
                .OrderByDescending(run => run.Order)
                .ThenByDescending(run => run.Started)
                .ThenBy(run => run.EvalRunId)
                .FirstOrDefaultAsync(),
            _ => throw new SharpOMaticException($"Move direction '{direction}' is not supported."),
        };

        if (adjacentRun is null)
        {
            await transaction.CommitAsync();
            return;
        }

        var currentOrder = evalRun.Order;
        var adjacentOrder = adjacentRun.Order;
        var sentinelOrder = await GetSentinelEvalRunOrder(dbContext, evalRun.EvalConfigId);

        evalRun.Order = sentinelOrder;
        await dbContext.SaveChangesAsync();

        adjacentRun.Order = currentOrder;
        evalRun.Order = adjacentOrder;
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DeleteEvalRun(Guid evalRunId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await (from run in dbContext.EvalRuns where run.EvalRunId == evalRunId select run).FirstOrDefaultAsync();
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        dbContext.Remove(evalRun);
        await dbContext.SaveChangesAsync();
    }

    public async Task<EvalRunDetail> GetEvalRunDetail(Guid evalRunId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.AsNoTracking().FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var evalRunSummary = new EvalRunSummary
        {
            EvalRunId = evalRun.EvalRunId,
            EvalConfigId = evalRun.EvalConfigId,
            Name = evalRun.Name,
            Order = evalRun.Order,
            Started = evalRun.Started,
            Finished = evalRun.Finished,
            Status = evalRun.Status,
            Message = evalRun.Message,
            Error = evalRun.Error,
            TotalRows = evalRun.TotalRows,
            CompletedRows = evalRun.CompletedRows,
            FailedRows = evalRun.FailedRows,
            AveragePassRate = evalRun.AveragePassRate,
            RunScoreMode = evalRun.RunScoreMode,
            Score = evalRun.Score,
        };

        var graderSummaries = await (
            from summary in dbContext.EvalRunGraderSummaries.AsNoTracking()
            join grader in dbContext.EvalGraders.AsNoTracking() on summary.EvalGraderId equals grader.EvalGraderId into graderJoin
            from grader in graderJoin.DefaultIfEmpty()
            where summary.EvalRunId == evalRunId
            orderby grader != null ? grader.Order : int.MaxValue, summary.EvalGraderId
            select new EvalRunGraderSummaryDetail
            {
                EvalRunGraderSummaryId = summary.EvalRunGraderSummaryId,
                EvalRunId = summary.EvalRunId,
                EvalGraderId = summary.EvalGraderId,
                Label = grader != null ? grader.Label : $"Grader {summary.EvalGraderId}",
                Order = grader != null ? grader.Order : int.MaxValue,
                PassThreshold = grader != null ? grader.PassThreshold : null,
                TotalCount = summary.TotalCount,
                CompletedCount = summary.CompletedCount,
                FailedCount = summary.FailedCount,
                MinScore = summary.MinScore,
                MaxScore = summary.MaxScore,
                AverageScore = summary.AverageScore,
                MedianScore = summary.MedianScore,
                StandardDeviation = summary.StandardDeviation,
                PassRate = summary.PassRate,
            }
        ).ToListAsync();

        return new EvalRunDetail { EvalRun = evalRunSummary, GraderSummaries = graderSummaries };
    }

    public async Task<int> GetEvalRunRowCount(Guid evalRunId, string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.AsNoTracking().FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var nameColumnId = await GetEvalRunNameColumnId(dbContext, evalRun.EvalConfigId);
        var rows = GetEvalRunRowProjections(dbContext, evalRunId, nameColumnId);
        rows = ApplyEvalRunRowSearch(rows, search);
        return await rows.CountAsync();
    }

    public async Task<List<EvalRunRowDetail>> GetEvalRunRows(Guid evalRunId, string? search, EvalRunRowSortField sortBy, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var evalRun = await dbContext.EvalRuns.AsNoTracking().FirstOrDefaultAsync(run => run.EvalRunId == evalRunId);
        if (evalRun is null)
            throw new SharpOMaticException($"EvalRun '{evalRunId}' cannot be found.");

        var nameColumnId = await GetEvalRunNameColumnId(dbContext, evalRun.EvalConfigId);
        var rows = GetEvalRunRowProjections(dbContext, evalRunId, nameColumnId);
        rows = ApplyEvalRunRowSearch(rows, search);
        rows = GetSortedEvalRunRows(rows, sortBy, sortDirection);

        if (skip > 0)
            rows = rows.Skip(skip);

        if (take > 0)
            rows = rows.Take(take);

        var rowPage = await rows.ToListAsync();

        var results = rowPage
            .Select(row => new EvalRunRowDetail
            {
                EvalRunRowId = row.EvalRunRowId,
                EvalRunId = row.EvalRunId,
                EvalRowId = row.EvalRowId,
                Name = row.Name,
                Order = row.Order,
                Status = row.Status,
                Score = row.Score,
                Started = row.Started,
                Finished = row.Finished,
                InputContext = row.InputContext,
                OutputContext = row.OutputContext,
                Error = row.Error,
                Graders = [],
            })
            .ToList();

        var evalRunRowIds = results.Select(row => row.EvalRunRowId).ToList();
        if (evalRunRowIds.Count == 0)
            return results;

        var graderDetails = await (
            from runRowGrader in dbContext.EvalRunRowGraders.AsNoTracking()
            join grader in dbContext.EvalGraders.AsNoTracking() on runRowGrader.EvalGraderId equals grader.EvalGraderId into graderJoin
            from grader in graderJoin.DefaultIfEmpty()
            where evalRunRowIds.Contains(runRowGrader.EvalRunRowId)
            orderby runRowGrader.EvalRunRowId, grader != null ? grader.Order : int.MaxValue, runRowGrader.EvalGraderId
            select new
            {
                runRowGrader.EvalRunRowId,
                Detail = new EvalRunRowGraderDetail
                {
                    EvalRunRowGraderId = runRowGrader.EvalRunRowGraderId,
                    EvalRunRowId = runRowGrader.EvalRunRowId,
                    EvalGraderId = runRowGrader.EvalGraderId,
                    Label = grader != null ? grader.Label : $"Grader {runRowGrader.EvalGraderId}",
                    Order = grader != null ? grader.Order : int.MaxValue,
                    Status = runRowGrader.Status,
                    Started = runRowGrader.Started,
                    Finished = runRowGrader.Finished,
                    Score = runRowGrader.Score,
                    InputContext = runRowGrader.InputContext,
                    OutputContext = runRowGrader.OutputContext,
                    Error = runRowGrader.Error,
                },
            }
        ).ToListAsync();

        var gradersByRowId = graderDetails.GroupBy(entry => entry.EvalRunRowId).ToDictionary(group => group.Key, group => group.Select(entry => entry.Detail).ToList());

        foreach (var row in results)
            if (gradersByRowId.TryGetValue(row.EvalRunRowId, out var graders))
                row.Graders = graders;

        return results;
    }

    private static IQueryable<EvalConfig> ApplyModelSearch(IQueryable<EvalConfig> evalConfigs, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return evalConfigs;

        var normalizedSearch = search.Trim().ToLower();
        return evalConfigs.Where(model => model.Name.ToLower().Contains(normalizedSearch));
    }

    private static IQueryable<EvalConfig> GetSortedModels(IQueryable<EvalConfig> models, EvalConfigSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            EvalConfigSortField.Description => sortDirection == SortDirection.Ascending
                ? models.OrderBy(model => model.Description).ThenBy(model => model.Name)
                : models.OrderByDescending(model => model.Description).ThenByDescending(model => model.Name),
            _ => sortDirection == SortDirection.Ascending
                ? models.OrderBy(model => model.Name).ThenBy(model => model.Description)
                : models.OrderByDescending(model => model.Name).ThenByDescending(model => model.Description),
        };
    }

    private static IQueryable<EvalRun> ApplyEvalRunSearch(IQueryable<EvalRun> evalRuns, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return evalRuns;

        var trimmedSearch = search.Trim();
        var normalizedSearch = trimmedSearch.ToLower();
        if (Guid.TryParse(trimmedSearch, out var parsedRunId))
        {
            return evalRuns.Where(run =>
                run.EvalRunId == parsedRunId
                || run.Name.ToLower().Contains(normalizedSearch)
                || (run.Message != null && run.Message.ToLower().Contains(normalizedSearch))
                || (run.Error != null && run.Error.ToLower().Contains(normalizedSearch))
            );
        }

        return evalRuns.Where(run =>
            run.Name.ToLower().Contains(normalizedSearch)
            || (run.Message != null && run.Message.ToLower().Contains(normalizedSearch))
            || (run.Error != null && run.Error.ToLower().Contains(normalizedSearch))
        );
    }

    private static IQueryable<EvalRun> GetSortedEvalRuns(IQueryable<EvalRun> evalRuns, EvalRunSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            EvalRunSortField.Order => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.Order).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId)
                : evalRuns.OrderByDescending(run => run.Order).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId),
            EvalRunSortField.Name => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.Name).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId)
                : evalRuns.OrderByDescending(run => run.Name).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId),
            EvalRunSortField.Status => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.Status).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId)
                : evalRuns.OrderByDescending(run => run.Status).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId),
            EvalRunSortField.CompletedRows => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.CompletedRows).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId)
                : evalRuns.OrderByDescending(run => run.CompletedRows).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId),
            EvalRunSortField.FailedRows => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.FailedRows).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId)
                : evalRuns.OrderByDescending(run => run.FailedRows).ThenByDescending(run => run.Started).ThenBy(run => run.EvalRunId),
            _ => sortDirection == SortDirection.Ascending
                ? evalRuns.OrderBy(run => run.Started).ThenBy(run => run.EvalRunId)
                : evalRuns.OrderByDescending(run => run.Started).ThenBy(run => run.EvalRunId),
        };
    }

    private static async Task<int> GetNextEvalRunOrder(SharpOMaticDbContext dbContext, Guid evalConfigId)
    {
        return (await dbContext.EvalRuns.Where(run => run.EvalConfigId == evalConfigId).MaxAsync(run => (int?)run.Order) ?? 0) + 1;
    }

    private static async Task<int> GetSentinelEvalRunOrder(SharpOMaticDbContext dbContext, Guid evalConfigId)
    {
        return (await dbContext.EvalRuns.Where(run => run.EvalConfigId == evalConfigId).MinAsync(run => (int?)run.Order) ?? 0) - 1;
    }

    private static bool IsUniqueEvalRunOrderViolation(DbUpdateException exception)
    {
        var current = exception.InnerException;
        while (current is not null)
        {
            var message = current.Message;
            if (
                message.Contains("EvalRuns", StringComparison.OrdinalIgnoreCase)
                && message.Contains("Order", StringComparison.OrdinalIgnoreCase)
                && (
                    message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private static async Task<Guid?> GetEvalRunNameColumnId(SharpOMaticDbContext dbContext, Guid evalConfigId)
    {
        var nameColumnId = await dbContext
            .EvalColumns.AsNoTracking()
            .Where(column => column.EvalConfigId == evalConfigId && column.Name.ToLower() == "name")
            .OrderBy(column => column.Order)
            .Select(column => (Guid?)column.EvalColumnId)
            .FirstOrDefaultAsync();

        if (nameColumnId.HasValue)
            return nameColumnId;

        return await dbContext
            .EvalColumns.AsNoTracking()
            .Where(column => column.EvalConfigId == evalConfigId)
            .OrderBy(column => column.Order)
            .Select(column => (Guid?)column.EvalColumnId)
            .FirstOrDefaultAsync();
    }

    private static IQueryable<EvalRunRowProjection> GetEvalRunRowProjections(SharpOMaticDbContext dbContext, Guid evalRunId, Guid? nameColumnId)
    {
        return (
            from runRow in dbContext.EvalRunRows.AsNoTracking()
            where runRow.EvalRunId == evalRunId
            select new EvalRunRowProjection
            {
                EvalRunRowId = runRow.EvalRunRowId,
                EvalRunId = runRow.EvalRunId,
                EvalRowId = runRow.EvalRowId,
                Name = nameColumnId.HasValue
                    ? (from data in dbContext.EvalData.AsNoTracking() where data.EvalRowId == runRow.EvalRowId && data.EvalColumnId == nameColumnId.Value select data.StringValue).FirstOrDefault()
                        ?? ""
                    : "",
                Order = runRow.Order,
                Status = runRow.Status,
                Score = runRow.Score,
                Started = runRow.Started,
                Finished = runRow.Finished,
                InputContext = runRow.InputContext,
                OutputContext = runRow.OutputContext,
                Error = runRow.Error,
            }
        );
    }

    private static IQueryable<EvalRunRowProjection> ApplyEvalRunRowSearch(IQueryable<EvalRunRowProjection> rows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return rows;

        var normalizedSearch = search.Trim().ToLower();
        return rows.Where(row => row.Name.ToLower().Contains(normalizedSearch) || (row.Error != null && row.Error.ToLower().Contains(normalizedSearch)));
    }

    private static IQueryable<EvalRunRowProjection> GetSortedEvalRunRows(IQueryable<EvalRunRowProjection> rows, EvalRunRowSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            EvalRunRowSortField.Order => sortDirection == SortDirection.Ascending
                ? rows.OrderBy(row => row.Order).ThenBy(row => row.Name)
                : rows.OrderByDescending(row => row.Order).ThenBy(row => row.Name),
            EvalRunRowSortField.Status => sortDirection == SortDirection.Ascending
                ? rows.OrderBy(row => row.Status).ThenBy(row => row.Order)
                : rows.OrderByDescending(row => row.Status).ThenBy(row => row.Order),
            EvalRunRowSortField.Started => sortDirection == SortDirection.Ascending
                ? rows.OrderBy(row => row.Started).ThenBy(row => row.Order)
                : rows.OrderByDescending(row => row.Started).ThenBy(row => row.Order),
            _ => sortDirection == SortDirection.Ascending ? rows.OrderBy(row => row.Name).ThenBy(row => row.Order) : rows.OrderByDescending(row => row.Name).ThenBy(row => row.Order),
        };
    }

    private sealed class EvalRunRowProjection
    {
        public required Guid EvalRunRowId { get; set; }
        public required Guid EvalRunId { get; set; }
        public required Guid EvalRowId { get; set; }
        public required string Name { get; set; }
        public required int Order { get; set; }
        public required EvalRunStatus Status { get; set; }
        public double? Score { get; set; }
        public required DateTime Started { get; set; }
        public DateTime? Finished { get; set; }
        public string? InputContext { get; set; }
        public string? OutputContext { get; set; }
        public string? Error { get; set; }
    }

    // ------------------------------------------------
    // Setting Operations
    // ------------------------------------------------

    public async Task<List<Setting>> GetSettings()
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from s in dbContext.Settings.AsNoTracking() orderby s.Name select s).ToListAsync();
    }

    public async Task<Setting?> GetSetting(string name)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await (from s in dbContext.Settings.AsNoTracking() where s.Name == name select s).FirstOrDefaultAsync();
    }

    public async Task UpsertSetting(Setting model)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var setting = await (from s in dbContext.Settings where s.Name == model.Name select s).FirstOrDefaultAsync();

        if (setting is null)
            dbContext.Settings.Add(model);
        else
            dbContext.Entry(setting).CurrentValues.SetValues(model);

        await dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------
    // Asset Operations
    // ------------------------------------------------
    public async Task<Asset> GetAsset(Guid assetId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var asset = await dbContext.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.AssetId == assetId);

        if (asset is null)
            throw new SharpOMaticException($"Asset '{assetId}' cannot be found.");

        return asset;
    }

    public async Task<int> GetAssetCount(AssetScope scope, string? search, Guid? runId = null, string? conversationId = null, Guid? folderId = null, bool topLevelOnly = false)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var assets = dbContext.Assets.AsNoTracking().Where(a => a.Scope == scope);

        if (scope == AssetScope.Run)
        {
            if (!runId.HasValue)
                throw new SharpOMaticException("Run asset queries require a runId.");

            assets = assets.Where(a => a.RunId == runId.Value);
        }
        else if (scope == AssetScope.Conversation)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new SharpOMaticException("Conversation asset queries require a conversationId.");

            assets = assets.Where(a => a.ConversationId == conversationId);
        }
        else if (scope == AssetScope.Library)
        {
            if (topLevelOnly)
                assets = assets.Where(a => a.FolderId == null);
            else if (folderId.HasValue)
                assets = assets.Where(a => a.FolderId == folderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var searchLower = normalizedSearch.ToLower();
            assets = assets.Where(a => a.Name.ToLower().Contains(searchLower));
        }

        return await assets.CountAsync();
    }

    public async Task<List<Asset>> GetAssetsByScope(
        AssetScope scope,
        string? search,
        AssetSortField sortBy,
        SortDirection sortDirection,
        int skip,
        int take,
        Guid? runId = null,
        string? conversationId = null,
        Guid? folderId = null,
        bool topLevelOnly = false
    )
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var assets = dbContext.Assets.AsNoTracking().Where(a => a.Scope == scope);

        if (scope == AssetScope.Run)
        {
            if (!runId.HasValue)
                throw new SharpOMaticException("Run asset queries require a runId.");

            assets = assets.Where(a => a.RunId == runId.Value);
        }
        else if (scope == AssetScope.Conversation)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new SharpOMaticException("Conversation asset queries require a conversationId.");

            assets = assets.Where(a => a.ConversationId == conversationId);
        }
        else if (scope == AssetScope.Library)
        {
            if (topLevelOnly)
                assets = assets.Where(a => a.FolderId == null);
            else if (folderId.HasValue)
                assets = assets.Where(a => a.FolderId == folderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var searchLower = normalizedSearch.ToLower();
            assets = assets.Where(a => a.Name.ToLower().Contains(searchLower));
        }

        var sortedAssets = GetSortedAssets(assets, sortBy, sortDirection);

        if (skip > 0)
            sortedAssets = sortedAssets.Skip(skip);

        if (take > 0)
            sortedAssets = sortedAssets.Take(take);

        return await sortedAssets.ToListAsync();
    }

    private static IQueryable<Asset> GetSortedAssets(IQueryable<Asset> assets, AssetSortField sortBy, SortDirection sortDirection)
    {
        return sortBy switch
        {
            AssetSortField.Name => sortDirection == SortDirection.Ascending
                ? assets.OrderBy(a => a.Name).ThenByDescending(a => a.Created)
                : assets.OrderByDescending(a => a.Name).ThenByDescending(a => a.Created),
            AssetSortField.Type => sortDirection == SortDirection.Ascending
                ? assets.OrderBy(a => a.MediaType).ThenByDescending(a => a.Created)
                : assets.OrderByDescending(a => a.MediaType).ThenByDescending(a => a.Created),
            AssetSortField.Size => sortDirection == SortDirection.Ascending
                ? assets.OrderBy(a => a.SizeBytes).ThenByDescending(a => a.Created)
                : assets.OrderByDescending(a => a.SizeBytes).ThenByDescending(a => a.Created),
            AssetSortField.Created => sortDirection == SortDirection.Ascending ? assets.OrderBy(a => a.Created).ThenBy(a => a.Name) : assets.OrderByDescending(a => a.Created).ThenBy(a => a.Name),
            _ => sortDirection == SortDirection.Ascending ? assets.OrderBy(a => a.Name).ThenByDescending(a => a.Created) : assets.OrderByDescending(a => a.Name).ThenByDescending(a => a.Created),
        };
    }

    public async Task<List<Asset>> GetRunAssets(Guid runId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        return await dbContext.Assets.AsNoTracking().Where(a => a.RunId == runId).OrderByDescending(a => a.Created).ToListAsync();
    }

    public async Task<Asset?> GetRunAssetByName(Guid runId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();
        var normalizedName = name.Trim().ToLower();

        return await dbContext
            .Assets.AsNoTracking()
            .Where(a => a.Scope == AssetScope.Run && a.RunId == runId && a.Name.ToLower() == normalizedName)
            .OrderByDescending(a => a.Created)
            .FirstOrDefaultAsync();
    }

    public async Task<Asset?> GetConversationAssetByName(string conversationId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();
        var normalizedName = name.Trim().ToLower();

        return await dbContext
            .Assets.AsNoTracking()
            .Where(a => a.Scope == AssetScope.Conversation && a.ConversationId == conversationId && a.Name.ToLower() == normalizedName)
            .OrderByDescending(a => a.Created)
            .FirstOrDefaultAsync();
    }

    public async Task<Asset?> GetLibraryAssetByFolderAndName(string folderName, string name)
    {
        if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(name))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();
        var normalizedFolderName = folderName.Trim();
        var normalizedAssetName = name.Trim().ToLower();

        return await (
            from asset in dbContext.Assets.AsNoTracking()
            join folder in dbContext.AssetFolders.AsNoTracking() on asset.FolderId equals folder.FolderId
            where asset.Scope == AssetScope.Library && folder.Name == normalizedFolderName && asset.Name.ToLower() == normalizedAssetName
            orderby asset.Created descending
            select asset
        ).FirstOrDefaultAsync();
    }

    public async Task<Asset?> GetLibraryAssetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();
        var normalizedName = name.Trim().ToLower();

        return await dbContext.Assets.AsNoTracking().Where(a => a.Scope == AssetScope.Library && a.Name.ToLower() == normalizedName).OrderByDescending(a => a.Created).FirstOrDefaultAsync();
    }

    public async Task UpsertAsset(Asset asset)
    {
        if (asset.Scope != AssetScope.Library && asset.FolderId.HasValue)
            throw new SharpOMaticException($"{asset.Scope} assets cannot be assigned to folders.");

        using var dbContext = dbContextFactory.CreateDbContext();

        var entity = await (from a in dbContext.Assets where a.AssetId == asset.AssetId select a).FirstOrDefaultAsync();

        if (entity is null)
            dbContext.Assets.Add(asset);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(asset);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsset(Guid assetId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var asset = await (from a in dbContext.Assets where a.AssetId == assetId select a).FirstOrDefaultAsync();

        if (asset is null)
            throw new SharpOMaticException($"Asset '{assetId}' cannot be found.");

        dbContext.Remove(asset);
        await dbContext.SaveChangesAsync();
    }

    public async Task<AssetFolder> GetAssetFolder(Guid folderId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var folder = await dbContext.AssetFolders.AsNoTracking().FirstOrDefaultAsync(f => f.FolderId == folderId);

        if (folder is null)
            throw new SharpOMaticException($"Asset folder '{folderId}' cannot be found.");

        return folder;
    }

    public async Task<AssetFolder?> GetAssetFolderByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        using var dbContext = dbContextFactory.CreateDbContext();
        var normalizedName = name.Trim();
        return await dbContext.AssetFolders.AsNoTracking().FirstOrDefaultAsync(f => f.Name == normalizedName);
    }

    public async Task<int> GetAssetFolderCount(string? search)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var folders = ApplyAssetFolderSearch(dbContext.AssetFolders.AsNoTracking(), search);
        return await folders.CountAsync();
    }

    public async Task<List<AssetFolder>> GetAssetFolders(string? search, SortDirection sortDirection, int skip, int take)
    {
        using var dbContext = dbContextFactory.CreateDbContext();

        var folders = ApplyAssetFolderSearch(dbContext.AssetFolders.AsNoTracking(), search);
        folders = sortDirection == SortDirection.Descending ? folders.OrderByDescending(f => f.Name).ThenByDescending(f => f.Created) : folders.OrderBy(f => f.Name).ThenBy(f => f.Created);

        if (skip > 0)
            folders = folders.Skip(skip);

        if (take > 0)
            folders = folders.Take(take);

        return await folders.ToListAsync();
    }

    public async Task<int> GetAssetFolderAssetCount(Guid folderId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        return await dbContext.Assets.AsNoTracking().Where(a => a.Scope == AssetScope.Library && a.FolderId == folderId).CountAsync();
    }

    public async Task UpsertAssetFolder(AssetFolder folder)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var entity = await dbContext.AssetFolders.FirstOrDefaultAsync(f => f.FolderId == folder.FolderId);

        if (entity is null)
            dbContext.AssetFolders.Add(folder);
        else
            dbContext.Entry(entity).CurrentValues.SetValues(folder);

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAssetFolder(Guid folderId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var folder = await dbContext.AssetFolders.FirstOrDefaultAsync(f => f.FolderId == folderId);
        if (folder is null)
            throw new SharpOMaticException($"Asset folder '{folderId}' cannot be found.");

        dbContext.Remove(folder);
        await dbContext.SaveChangesAsync();
    }

    private static IQueryable<AssetFolder> ApplyAssetFolderSearch(IQueryable<AssetFolder> folders, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return folders;

        var normalizedSearch = search.Trim().ToLower();
        return folders.Where(folder => folder.Name.ToLower().Contains(normalizedSearch));
    }

    private async Task DeleteAssetStorageForRuns(IReadOnlyCollection<Guid> runIds)
    {
        if (assetStore is null || runIds.Count == 0)
            return;

        using var dbContext = dbContextFactory.CreateDbContext();

        var storageKeys = await dbContext.Assets.AsNoTracking().Where(a => a.RunId.HasValue && runIds.Contains(a.RunId.Value)).Select(a => a.StorageKey).ToListAsync();

        foreach (var storageKey in storageKeys)
            await assetStore.DeleteAsync(storageKey);
    }

    private async Task DeleteAssetStorageForConversations(IReadOnlyCollection<string> conversationIds)
    {
        if (assetStore is null || conversationIds.Count == 0)
            return;

        using var dbContext = dbContextFactory.CreateDbContext();

        var storageKeys = await dbContext.Assets.AsNoTracking().Where(a => a.ConversationId != null && conversationIds.Contains(a.ConversationId)).Select(a => a.StorageKey).ToListAsync();

        foreach (var storageKey in storageKeys)
            await assetStore.DeleteAsync(storageKey);
    }
}
