
namespace SharpOMatic.Engine.Services;

public class EngineService(
    IServiceScopeFactory ScopeFactory,
    INodeQueueService QueueService,
    IRepositoryService RepositoryService,
    IScriptOptionsService ScriptOptionsService,
    IJsonConverterService JsonConverterService
) : IEngineService
{
    private const int ConversationLeaseMinutes = 5;

    private static void ValidateWorkflowExecutionMode(WorkflowEntity workflow, bool allowConversation)
    {
        if (allowConversation)
        {
            if (!workflow.IsConversationEnabled)
                throw new SharpOMaticException("Workflow is not enabled for conversations.");

            return;
        }

        if (workflow.IsConversationEnabled)
            throw new SharpOMaticException("Conversation-enabled workflows must be started through conversation APIs.");
    }

    public async Task<Guid> GetWorkflowId(string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new SharpOMaticException("Workflow name cannot be empty or whitespace.");

        var summaries = await RepositoryService.GetWorkflowSummaries();
        var matches = summaries.Where(w => w.Name == workflowName).Take(2).ToList();

        if (matches.Count == 0)
            throw new SharpOMaticException("There is no matching workflow for this name.");

        if (matches.Count > 1)
            throw new SharpOMaticException("There is more than one matching workflow for this name.");

        return matches[0].Id;
    }

    public async Task<Run> StartWorkflowRunAndWait(
        Guid workflowId,
        ContextObject? nodeContext = null,
        ContextEntryListEntity? inputEntries = null,
        bool needsEditorEvents = false
    )
    {
        var completionSource = new TaskCompletionSource<Run>(TaskCreationOptions.RunContinuationsAsynchronously);
        return await StartWorkflowRunInternal(workflowId, nodeContext, inputEntries, needsEditorEvents, completionSource, waitForCompletion: true);
    }

    public async Task<Guid> StartWorkflowRunAndNotify(
        Guid workflowId,
        ContextObject? nodeContext = null,
        ContextEntryListEntity? inputEntries = null,
        bool needsEditorEvents = false
    )
    {
        var completionSource = new TaskCompletionSource<Run>(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = await StartWorkflowRunInternal(workflowId, nodeContext, inputEntries, needsEditorEvents, completionSource, waitForCompletion: false);
        return run.RunId;
    }

    public Run StartWorkflowRunSynchronously(
        Guid workflowId,
        ContextObject? context = null,
        ContextEntryListEntity? inputEntries = null,
        bool needsEditorEvents = false
    )
    {
        return StartWorkflowRunAndWait(workflowId, context, inputEntries, needsEditorEvents).GetAwaiter().GetResult();
    }

    public async Task<EvalRun> StartEvalRun(Guid evalConfigId, string? name = null, int? sampleCount = null)
    {
        return await StartEvalRunInternal(evalConfigId, name, sampleCount);
    }

    public Task<Run> StartOrResumeConversationAndWait(Guid workflowId, string conversationId, NodeResumeInput? resumeInput = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false, string? streamConversationId = null)
    {
        var completionSource = new TaskCompletionSource<Run>(TaskCreationOptions.RunContinuationsAsynchronously);
        return StartOrResumeConversationInternal(
            workflowId,
            conversationId,
            resumeInput,
            inputEntries,
            needsEditorEvents,
            completionSource,
            waitForCompletion: true,
            streamConversationId: streamConversationId
        );
    }

    public async Task<Guid> StartOrResumeConversationAndNotify(Guid workflowId, string conversationId, NodeResumeInput? resumeInput = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false, string? streamConversationId = null)
    {
        var completionSource = new TaskCompletionSource<Run>(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = await StartOrResumeConversationInternal(
            workflowId,
            conversationId,
            resumeInput,
            inputEntries,
            needsEditorEvents,
            completionSource,
            waitForCompletion: false,
            streamConversationId: streamConversationId
        );
        return run.RunId;
    }

    public Run StartOrResumeConversationSynchronously(Guid workflowId, string conversationId, NodeResumeInput? resumeInput = null, ContextEntryListEntity? inputEntries = null, bool needsEditorEvents = false, string? streamConversationId = null)
    {
        return StartOrResumeConversationAndWait(workflowId, conversationId, resumeInput, inputEntries, needsEditorEvents, streamConversationId).GetAwaiter().GetResult();
    }

    private async Task<Run> StartOrResumeConversationInternal(
        Guid workflowId,
        string conversationId,
        NodeResumeInput? resumeInput,
        ContextEntryListEntity? inputEntries,
        bool needsEditorEvents,
        TaskCompletionSource<Run> completionSource,
        bool waitForCompletion,
        string? streamConversationId
    )
    {
        var leaseOwner = Guid.NewGuid().ToString("N");
        resumeInput ??= new ContinueResumeInput();
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new SharpOMaticException("Conversation id cannot be empty or whitespace.");

        conversationId = conversationId.Trim();
        var conversation = await RepositoryService.GetConversation(conversationId);
        if (conversation is null)
        {
            var rootWorkflow = await RepositoryService.GetWorkflow(workflowId);
            ValidateConversationWorkflow(rootWorkflow);

            conversation = new Conversation()
            {
                ConversationId = conversationId,
                WorkflowId = workflowId,
                Status = ConversationStatus.Created,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                CurrentTurnNumber = 0,
            };
            await RepositoryService.UpsertConversation(conversation);
        }
        else if (conversation.WorkflowId != workflowId)
        {
            throw new SharpOMaticException("Conversation id does not belong to the requested workflow.");
        }

        if (!await RepositoryService.TryAcquireConversationLease(conversationId, leaseOwner, DateTime.UtcNow.AddMinutes(ConversationLeaseMinutes)))
            throw new SharpOMaticException("Conversation is already running.");

        conversation = await RepositoryService.GetConversation(conversationId) ?? conversation;
        var previousCheckpoint = await RepositoryService.GetConversationCheckpoint(conversationId);
        var turnNumber = conversation.CurrentTurnNumber + 1;

        var run = new Run()
        {
            WorkflowId = workflowId,
            ConversationId = conversationId,
            TurnNumber = turnNumber,
            RunId = Guid.NewGuid(),
            RunStatus = RunStatus.Created,
            NeedsEditorEvents = needsEditorEvents,
            Message = "Created",
            Created = DateTime.Now,
            InputContext = JsonSerializer.Serialize(new ContextObject(), new JsonSerializerOptions().BuildOptions(JsonConverterService.GetConverters())),
        };
        await RepositoryService.UpsertRun(run);

        var nodeRunLimitSetting = await RepositoryService.GetSetting("RunNodeLimit");
        var nodeRunLimit = nodeRunLimitSetting?.ValueInteger ?? NodeExecutionService.DEFAULT_NODE_RUN_LIMIT;

        var serviceScope = ScopeFactory.CreateScope();
        try
        {
            var processContext = new ProcessContext(
                serviceScope,
                run,
                nodeRunLimit,
                completionSource,
                conversation,
                previousCheckpoint,
                resumeInput,
                turnNumber,
                leaseOwner,
                streamConversationId,
                DeserializeWorkflowSnapshots(previousCheckpoint?.WorkflowSnapshotsJson)
            );
            processContext.InitializeStreamSequence(await RepositoryService.GetNextStreamSequence(run.RunId, processContext.StreamConversationId));

            NextNodeData nextNode;
            if (conversation.Status == ConversationStatus.Suspended)
            {
                if (inputEntries?.Entries.Length > 0)
                    throw new SharpOMaticException("Suspended conversations cannot accept start input entries.");

                nextNode = await BuildSuspendedTurnStart(processContext, workflowId, previousCheckpoint, resumeInput);
            }
            else
                nextNode = await BuildFreshTurnStart(processContext, workflowId, previousCheckpoint, resumeInput, inputEntries);

            conversation.Status = ConversationStatus.Running;
            conversation.Updated = DateTime.UtcNow;
            conversation.CurrentTurnNumber = turnNumber;
            conversation.LastRunId = run.RunId;
            conversation.LastError = null;
            await RepositoryService.UpsertConversation(conversation);

            await processContext.RunUpdated();
            QueueService.Enqueue(nextNode);

            if (waitForCompletion)
                return await completionSource.Task.ConfigureAwait(false);

            return run;
        }
        catch
        {
            serviceScope.Dispose();
            await RepositoryService.ReleaseConversationLease(conversation.ConversationId, leaseOwner);
            throw;
        }
    }

    private async Task<Run> CreateRunInternal(Guid workflowId, bool needsEditorEvents)
    {
        var workflow = await RepositoryService.GetWorkflow(workflowId);
        if (workflow is null)
            throw new SharpOMaticException($"Could not load workflow {workflowId}.");

        ValidateWorkflowExecutionMode(workflow, allowConversation: false);

        if (workflow.Nodes.Count(n => n.NodeType == NodeType.Start) != 1)
            throw new SharpOMaticException("Must have exactly one start node.");

        var converters = JsonConverterService.GetConverters();
        var inputContext = new ContextObject();

        var run = new Run()
        {
            WorkflowId = workflowId,
            RunId = Guid.NewGuid(),
            RunStatus = RunStatus.Created,
            NeedsEditorEvents = needsEditorEvents,
            Message = "Created",
            Created = DateTime.Now,
            InputContext = JsonSerializer.Serialize(inputContext, new JsonSerializerOptions().BuildOptions(converters)),
        };

        await RepositoryService.UpsertRun(run);
        return run;
    }

    private async Task<Run> StartWorkflowRunInternal(
        Guid workflowId,
        ContextObject? nodeContext,
        ContextEntryListEntity? inputEntries,
        bool needsEditorEvents,
        TaskCompletionSource<Run> completionSource,
        bool waitForCompletion
    )
    {
        var run = await CreateRunInternal(workflowId, needsEditorEvents);
        await StartRunInternal(run, nodeContext, inputEntries, completionSource);

        if (waitForCompletion)
            return await completionSource.Task.ConfigureAwait(false);

        return run;
    }

    private async Task<Run> StartCreatedRunAndWait(Run run, ContextObject? nodeContext = null, ContextEntryListEntity? inputEntries = null)
    {
        var completionSource = new TaskCompletionSource<Run>(TaskCreationOptions.RunContinuationsAsynchronously);
        await StartRunInternal(run, nodeContext, inputEntries, completionSource);
        return await completionSource.Task.ConfigureAwait(false);
    }

    private async Task StartRunInternal(Run run, ContextObject? nodeContext, ContextEntryListEntity? inputEntries, TaskCompletionSource<Run>? completionSource)
    {
        nodeContext ??= [];

        var serviceScope = ScopeFactory.CreateScope();
        try
        {
            var inputJson = await ApplyInputEntries(serviceScope.ServiceProvider, nodeContext, inputEntries, run.RunId);

            var workflow = await RepositoryService.GetWorkflow(run.WorkflowId) ?? throw new SharpOMaticException($"Could not load workflow {run.WorkflowId}.");
            ValidateWorkflowExecutionMode(workflow, allowConversation: false);
            var currentNodes = workflow.Nodes.Where(n => n.NodeType == NodeType.Start).ToList();
            if (currentNodes.Count != 1)
                throw new SharpOMaticException("Must have exactly one start node.");

            var converters = JsonConverterService.GetConverters();
            run.InputEntries = inputJson;
            run.InputContext = JsonSerializer.Serialize(nodeContext, new JsonSerializerOptions().BuildOptions(converters));

            var nodeRunLimitSetting = await RepositoryService.GetSetting("RunNodeLimit");
            var nodeRunLimit = nodeRunLimitSetting?.ValueInteger ?? NodeExecutionService.DEFAULT_NODE_RUN_LIMIT;

            var processContext = new ProcessContext(serviceScope, run, nodeRunLimit, completionSource);
            processContext.InitializeStreamSequence(await RepositoryService.GetNextStreamSequence(run.RunId, processContext.StreamConversationId));
            var workflowContext = new WorkflowContext(processContext, workflow);
            var threadContext = processContext.CreateThread(nodeContext, workflowContext);
            await processContext.RunUpdated();
            QueueService.Enqueue(threadContext, currentNodes[0]);
        }
        catch
        {
            serviceScope.Dispose();
            throw;
        }
    }

    private async Task<string?> ApplyInputEntries(IServiceProvider serviceProvider, ContextObject nodeContext, ContextEntryListEntity? inputEntries, Guid runId)
    {
        if (inputEntries is null)
            return null;

        var inputJson = JsonSerializer.Serialize(inputEntries);
        foreach (var entry in inputEntries.Entries)
        {
            var entryValue = await ContextHelpers.ResolveContextEntryValue(serviceProvider, nodeContext, entry, ScriptOptionsService, runId);
            if (!nodeContext.TrySet(entry.InputPath, entryValue))
                throw new SharpOMaticException($"Input entry '{entry.InputPath}' could not be assigned the value.");
        }

        return inputJson;
    }

    private async Task<EvalRun> StartEvalRunInternal(Guid evalConfigId, string? name, int? sampleCount)
    {
        var evalConfigDetail = await RepositoryService.GetEvalConfigDetail(evalConfigId);
        var allRows = evalConfigDetail.Rows.OrderBy(r => r.Order).ToList();
        var selectedRows = ResolveEvalRowsForRun(allRows, sampleCount);
        var started = DateTime.Now;

        var evalRun = new EvalRun
        {
            EvalRunId = Guid.NewGuid(),
            EvalConfigId = evalConfigId,
            Name = ResolveEvalRunName(name, started),
            Order = 0,
            Started = started,
            Finished = null,
            Status = EvalRunStatus.Running,
            Message = "Starting",
            Error = null,
            CancelRequested = false,
            TotalRows = selectedRows.Count,
            CompletedRows = 0,
            FailedRows = 0,
            AveragePassRate = null,
            RunScoreMode = evalConfigDetail.EvalConfig.RunScoreMode,
            Score = null,
        };

        await RepositoryService.UpsertEvalRun(evalRun);
        _ = Task.Run(() => PerformEvalRun(evalConfigDetail, evalRun, selectedRows));

        return evalRun;
    }

    private async Task PerformEvalRun(EvalConfigDetail evalConfigDetail, EvalRun evalRun, IReadOnlyList<EvalRow> selectedRows)
    {
        using var serviceScope = ScopeFactory.CreateScope();
        var provider = serviceScope.ServiceProvider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        var jsonConverterService = provider.GetRequiredService<IJsonConverterService>();
        var assetStore = provider.GetRequiredService<IAssetStore>();
        var scriptOptionsService = provider.GetRequiredService<IScriptOptionsService>();
        var engineNotifications = provider.GetServices<IEngineNotification>();
        var progressServices = provider.GetServices<IProgressService>();

        var configuredMaxParallel = evalConfigDetail.EvalConfig.MaxParallel;
        var normalizedMaxParallel = Math.Max(1, Math.Min(selectedRows.Count, Math.Max(1, configuredMaxParallel)));
        using var progressUpdateGate = new SemaphoreSlim(1, 1);
        var cancelRequested = 0;
        var runDeleted = 0;

        try
        {
            await Parallel.ForEachAsync(
                selectedRows,
                new ParallelOptions { MaxDegreeOfParallelism = normalizedMaxParallel },
                async (row, cancellationToken) =>
                {
                    if (Volatile.Read(ref cancelRequested) == 1 || Volatile.Read(ref runDeleted) == 1)
                        return;

                    EvalRun evalRunState;
                    try
                    {
                        evalRunState = await repository.GetEvalRun(evalRun.EvalRunId);
                    }
                    catch (SharpOMaticException ex) when (IsMissingEvalRun(ex, evalRun.EvalRunId))
                    {
                        Interlocked.Exchange(ref runDeleted, 1);
                        Interlocked.Exchange(ref cancelRequested, 1);
                        return;
                    }

                    if (evalRunState.CancelRequested)
                    {
                        Interlocked.Exchange(ref cancelRequested, 1);
                        return;
                    }

                    var evalRunRow = await PerformEvalRow(provider, repository, jsonConverterService, assetStore, scriptOptionsService, evalRun.EvalRunId, evalConfigDetail, row);
                    if (evalRunRow is null)
                    {
                        Interlocked.Exchange(ref runDeleted, 1);
                        Interlocked.Exchange(ref cancelRequested, 1);
                        return;
                    }

                    await progressUpdateGate.WaitAsync(cancellationToken);
                    try
                    {
                        if (evalRunRow.Status == EvalRunStatus.Completed)
                            evalRun.CompletedRows++;
                        else
                            evalRun.FailedRows++;

                        if (!await repository.UpsertEvalRun(evalRun, allowInsert: false))
                        {
                            Interlocked.Exchange(ref runDeleted, 1);
                            Interlocked.Exchange(ref cancelRequested, 1);
                            return;
                        }

                        foreach (var progressService in progressServices)
                            await progressService.EvalRunProgress(evalRun);
                    }
                    finally
                    {
                        progressUpdateGate.Release();
                    }
                }
            );

            if (Volatile.Read(ref runDeleted) == 1)
                return;

            var isCancelRequested = Volatile.Read(ref cancelRequested) == 1;
            if (!isCancelRequested)
            {
                EvalRun latestRun;
                try
                {
                    latestRun = await repository.GetEvalRun(evalRun.EvalRunId);
                }
                catch (SharpOMaticException ex) when (IsMissingEvalRun(ex, evalRun.EvalRunId))
                {
                    Interlocked.Exchange(ref runDeleted, 1);
                    return;
                }

                isCancelRequested = latestRun.CancelRequested;
            }

            List<EvalRunGraderSummary> graderSummaries = [];
            foreach (var evalGrader in evalConfigDetail.Graders.OrderBy(g => g.Order))
            {
                var runRowGraders = await repository.GetEvalRunRowGraders(evalRun.EvalRunId, evalGrader.EvalGraderId);
                var totalCount = runRowGraders.Count;
                var completedCount = runRowGraders.Count(grader => grader.Status == EvalRunStatus.Completed);
                var failedCount = runRowGraders.Count(grader => grader.Status == EvalRunStatus.Failed);

                var scoredValues = runRowGraders
                    .Where(grader => grader.Status == EvalRunStatus.Completed && grader.Score.HasValue)
                    .Select(grader => grader.Score!.Value)
                    .OrderBy(score => score)
                    .ToList();

                double? minScore = null;
                double? maxScore = null;
                double? averageScore = null;
                double? medianScore = null;
                double? standardDeviation = null;
                double? passRate = null;

                if (scoredValues.Count > 0)
                {
                    var scoredCount = scoredValues.Count;
                    minScore = scoredValues[0];
                    maxScore = scoredValues[scoredCount - 1];
                    averageScore = scoredValues.Average();

                    if (scoredCount % 2 == 1)
                        medianScore = scoredValues[scoredCount / 2];
                    else
                        medianScore = (scoredValues[(scoredCount / 2) - 1] + scoredValues[scoredCount / 2]) / 2.0;

                    var mean = averageScore.Value;
                    var variance = scoredValues.Sum(score => Math.Pow(score - mean, 2)) / scoredCount;
                    standardDeviation = Math.Sqrt(variance);

                    var passingCount = scoredValues.Count(score => score >= evalGrader.PassThreshold);
                    passRate = passingCount / (double)scoredCount;
                }

                graderSummaries.Add(
                    new EvalRunGraderSummary
                    {
                        EvalRunGraderSummaryId = Guid.NewGuid(),
                        EvalRunId = evalRun.EvalRunId,
                        EvalGraderId = evalGrader.EvalGraderId,
                        TotalCount = totalCount,
                        CompletedCount = completedCount,
                        FailedCount = failedCount,
                        MinScore = minScore,
                        MaxScore = maxScore,
                        AverageScore = averageScore,
                        MedianScore = medianScore,
                        StandardDeviation = standardDeviation,
                        PassRate = passRate,
                    }
                );
            }

            await repository.UpsertEvalRunGraderSummaries(graderSummaries);
            evalRun.AveragePassRate = graderSummaries.Select(summary => summary.PassRate).Average();
            evalRun.Score = CalculateEvalRunScore(evalRun.RunScoreMode, evalConfigDetail.Graders, graderSummaries);

            if (isCancelRequested)
            {
                evalRun.Message = "Canceled";
                evalRun.Error = null;
                evalRun.Status = EvalRunStatus.Canceled;
            }
            else
            {
                evalRun.Message = "Completed";
                evalRun.Status = EvalRunStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            if (!await EvalRunExists(repository, evalRun.EvalRunId))
            {
                Interlocked.Exchange(ref runDeleted, 1);
                return;
            }

            evalRun.Message = "Failed";
            evalRun.Error = ex.Message;
            evalRun.Status = EvalRunStatus.Failed;
        }
        finally
        {
            if (Volatile.Read(ref runDeleted) == 0)
            {
                evalRun.Finished = DateTime.Now;
                var saved = await repository.UpsertEvalRun(evalRun, allowInsert: false);
                if (saved)
                {
                    foreach (var progressService in progressServices)
                        await progressService.EvalRunProgress(evalRun);

                    foreach (var notification in engineNotifications)
                    {
                        // Notify in separate task in case called instance performs a long running action
                        _ = Task.Run(async () =>
                        {
                            await notification.EvalRunCompleted(evalRun.EvalRunId, evalRun.Status, evalRun.Error);
                        });
                    }
                }
            }
        }
    }

    private async Task<EvalRunRow?> PerformEvalRow(
        IServiceProvider serviceProvider,
        IRepositoryService repository,
        IJsonConverterService jsonConverterService,
        IAssetStore assetStore,
        IScriptOptionsService scriptOptionsService,
        Guid evalRunId,
        EvalConfigDetail evalConfigDetail,
        EvalRow evalRow
    )
    {
        var evalRunRow = new EvalRunRow
        {
            EvalRunRowId = Guid.NewGuid(),
            EvalRunId = evalRunId,
            EvalRowId = evalRow.EvalRowId,
            Order = evalRow.Order,
            Started = DateTime.Now,
            Status = EvalRunStatus.Running,
        };

        if (!await repository.UpsertEvalRunRows([evalRunRow]))
            return null;

        try
        {
            var rowData = evalConfigDetail.Data.Where(d => d.EvalRowId == evalRow.EvalRowId).ToList();

            var nameColumn = evalConfigDetail.Columns.Where(c => c.Order == 0).FirstOrDefault();
            if (nameColumn is null)
                throw new SharpOMaticException($"Row '{evalRow.Order}' does not have mandatory 'Name' column.");

            var nameData = rowData.Where(r => r.EvalColumnId == nameColumn.EvalColumnId).FirstOrDefault();
            if (nameData is null)
                throw new SharpOMaticException($"Row '{evalRow.Order}' does not have mandatory 'Name' column.");

            var rowName = nameData.StringValue ?? "Unnamed";
            var run = await CreateRunInternal(evalConfigDetail.EvalConfig.WorkflowId ?? Guid.Empty, needsEditorEvents: false);
            ContextObject inputContext = [];
            foreach (var column in evalConfigDetail.Columns.Where(c => c.Order > 0))
            {
                var columnData = rowData.Where(r => r.EvalColumnId == column.EvalColumnId).FirstOrDefault();
                if (columnData is not null)
                {
                    switch (column.EntryType)
                    {
                        case ContextEntryType.Bool:
                            if (columnData.BoolValue.HasValue)
                                inputContext.Set(ContextPath(column), columnData.BoolValue.Value);
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing bool value.");
                            break;
                        case ContextEntryType.Int:
                            if (columnData.IntValue.HasValue)
                                inputContext.Set(ContextPath(column), columnData.IntValue.Value);
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing int value.");
                            break;
                        case ContextEntryType.Double:
                            if (columnData.DoubleValue.HasValue)
                                inputContext.Set(ContextPath(column), columnData.DoubleValue.Value);
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing double value.");
                            break;
                        case ContextEntryType.String:
                            if (columnData.StringValue is not null)
                                inputContext.Set(ContextPath(column), columnData.StringValue);
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing string value.");
                            break;
                        case ContextEntryType.JSON:
                            if (!string.IsNullOrWhiteSpace(columnData.StringValue))
                            {
                                try
                                {
                                    var obj = ContextHelpers.FastDeserializeString(columnData.StringValue);
                                    inputContext.Set(ContextPath(column), obj);
                                }
                                catch
                                {
                                    throw new SharpOMaticException($"Column '{column.Name}' for row '{rowName}' could not be parsed as json.");
                                }
                            }
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing json value.");
                            break;
                        case ContextEntryType.Expression:
                            if (!string.IsNullOrWhiteSpace(columnData.StringValue))
                            {
                                var options = scriptOptionsService.GetScriptOptions();
                                    var globals = new ScriptCodeContext()
                                    {
                                        Context = [],
                                        ServiceProvider = serviceProvider,
                                        Assets = new AssetHelper(repository, assetStore, run.RunId),
                                    };

                                try
                                {
                                    var result = await CSharpScript.EvaluateAsync(columnData.StringValue, options, globals, typeof(ScriptCodeContext));
                                    inputContext.Set(ContextPath(column), result);
                                }
                                catch (CompilationErrorException e1)
                                {
                                    // Return the first 3 errors only
                                    StringBuilder sb = new();
                                    sb.AppendLine($"Column '{column.Name}' for row '{rowName}' expression failed compilation.\n");
                                    foreach (var diagnostic in e1.Diagnostics.Take(3))
                                        sb.AppendLine(diagnostic.ToString());

                                    throw new SharpOMaticException(sb.ToString());
                                }
                                catch (InvalidOperationException e2)
                                {
                                    StringBuilder sb = new();
                                    sb.AppendLine($"Column '{column.Name}' for row '{rowName}' expression failed during execution.\n");
                                    sb.Append(e2.Message);
                                    throw new SharpOMaticException(sb.ToString());
                                }
                            }
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing expression value.");
                            break;
                        case ContextEntryType.AssetRef:
                            if (columnData.StringValue is not null)
                            {
                                var assetRef = ContextHelpers.ParseAssetRef(columnData.StringValue, EngineService.ContextPath(column));
                                inputContext.Set(ContextPath(column), assetRef);
                            }
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing asset reference value.");
                            break;
                        case ContextEntryType.AssetRefList:
                            if (columnData.StringValue is not null)
                            {
                                var assetRef = ContextHelpers.ParseAssetRefList(columnData.StringValue, EngineService.ContextPath(column));
                                inputContext.Set(ContextPath(column), assetRef);
                            }
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing asset reference list value.");
                            break;
                        default:
                            throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has unrecognized type '{column.EntryType}'.");
                    }
                }
                else if (!column.Optional)
                    throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has a missing value.");
            }

            var serializedInput = inputContext.Serialize(jsonConverterService);
            evalRunRow.InputContext = serializedInput;
            var runResult = await StartCreatedRunAndWait(run, inputContext);
            if (runResult.RunStatus == RunStatus.Failed)
            {
                evalRunRow.OutputContext = runResult.OutputContext;
                evalRunRow.Error = runResult.Error;
                evalRunRow.Status = EvalRunStatus.Failed;
            }
            else
            {
                inputContext = ContextObject.Deserialize(serializedInput, jsonConverterService);
                var resultContext = ContextObject.Deserialize(runResult.OutputContext, jsonConverterService);
                ContextHelpers.OverwriteContexts(inputContext, resultContext);
                var graderContext = inputContext.Serialize(jsonConverterService);
                List<EvalRunRowGrader> graderResults = [];

                foreach (var evalGrader in evalConfigDetail.Graders.OrderBy(g => g.Order))
                    graderResults.Add(await PerformEvalGrader(repository, jsonConverterService, evalRunId, evalRunRow.EvalRunRowId, evalGrader, graderContext));

                evalRunRow.Score = CalculateEvalRunRowScore(evalConfigDetail.EvalConfig.RowScoreMode, graderResults);
                evalRunRow.OutputContext = runResult.OutputContext;
                evalRunRow.Status = EvalRunStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            evalRunRow.Error = ex.Message;
            evalRunRow.Status = EvalRunStatus.Failed;
        }
        finally
        {
            evalRunRow.Finished = DateTime.Now;
            await repository.UpsertEvalRunRows([evalRunRow], allowInsert: false);
        }

        return evalRunRow;
    }

    private async Task<EvalRunRowGrader> PerformEvalGrader(IRepositoryService repository, IJsonConverterService jsonConverterService, Guid evalRunId, Guid evalRunRowId, EvalGrader evalGrader, string? jsonContext)
    {
        var evalRunRowGrader = new EvalRunRowGrader
        {
            EvalRunRowGraderId = Guid.NewGuid(),
            EvalRunRowId = evalRunRowId,
            EvalGraderId = evalGrader.EvalGraderId,
            EvalRunId = evalRunId,
            Started = DateTime.Now,
            Status = EvalRunStatus.Running,
        };

        try
        {
            ContextObject inputContext = [];
            if (!string.IsNullOrWhiteSpace(jsonContext))
                inputContext = ContextObject.Deserialize(jsonContext, jsonConverterService);

            var runResult = await StartWorkflowRunAndWait(evalGrader.WorkflowId ?? Guid.Empty, inputContext);
            evalRunRowGrader.InputContext = jsonContext;
            evalRunRowGrader.OutputContext = runResult.OutputContext;

            if (runResult.RunStatus == RunStatus.Failed)
            {
                evalRunRowGrader.Error = runResult.Error;
                evalRunRowGrader.Status = EvalRunStatus.Failed;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(runResult.OutputContext))
                {
                    var graderOutput = ContextObject.Deserialize(runResult.OutputContext, jsonConverterService);
                    if (TryGetEvalScore(graderOutput, out var score))
                        evalRunRowGrader.Score = score;
                }

                evalRunRowGrader.Status = EvalRunStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            evalRunRowGrader.Error = ex.Message;
            evalRunRowGrader.Status = EvalRunStatus.Failed;
        }
        finally
        {
            evalRunRowGrader.Finished = DateTime.Now;
            await repository.UpsertEvalRunRowGraders([evalRunRowGrader]);
        }

        return evalRunRowGrader;
    }

    private static double? CalculateEvalRunScore(EvalRunScoreMode runScoreMode, IReadOnlyList<EvalGrader> graders, List<EvalRunGraderSummary> graderSummaries)
    {
        var selectedGraderIds = graders
            .Where(grader => grader.IncludeInScore)
            .Select(grader => grader.EvalGraderId)
            .ToHashSet();

        if (selectedGraderIds.Count == 0)
            return null;

        var metricValues = graderSummaries
            .Where(summary => selectedGraderIds.Contains(summary.EvalGraderId))
            .Select(summary => runScoreMode switch
            {
                EvalRunScoreMode.AverageScore => summary.AverageScore,
                EvalRunScoreMode.MinimumScore => summary.MinScore,
                EvalRunScoreMode.MaximumScore => summary.MaxScore,
                EvalRunScoreMode.PassRate => summary.PassRate,
                _ => summary.AverageScore,
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (metricValues.Count == 0)
            return null;

        return metricValues.Average();
    }

    private static bool TryGetEvalScore(ContextObject graderOutput, out double score)
    {
        if (graderOutput.TryGet("score", out score))
            return true;

        if (graderOutput.TryGet<float>("score", out var floatScore))
        {
            score = floatScore;
            return true;
        }

        if (graderOutput.TryGet<decimal>("score", out var decimalScore))
        {
            score = (double)decimalScore;
            return true;
        }

        if (graderOutput.TryGet<int>("score", out var intScore))
        {
            score = intScore;
            return true;
        }

        if (graderOutput.TryGet<string>("score", out var stringScore) && !string.IsNullOrWhiteSpace(stringScore))
        {
            if (double.TryParse(stringScore, out score))
                return true;

            if (
                double.TryParse(
                    stringScore,
                    System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out score
                )
            )
            {
                return true;
            }
        }

        score = default;
        return false;
    }

    private static double? CalculateEvalRunRowScore(EvalRunRowScoreMode rowScoreMode, List<EvalRunRowGrader> graderResults)
    {
        if (graderResults.Count == 0)
            return null;

        if (rowScoreMode == EvalRunRowScoreMode.FirstGrader)
            return graderResults[0].Score;

        var scoredValues = graderResults.Where(grader => grader.Score.HasValue).Select(grader => grader.Score!.Value).ToList();
        if (scoredValues.Count == 0)
            return null;

        return rowScoreMode switch
        {
            EvalRunRowScoreMode.Average => scoredValues.Average(),
            EvalRunRowScoreMode.Minimum => scoredValues.Min(),
            EvalRunRowScoreMode.Maximum => scoredValues.Max(),
            _ => graderResults[0].Score,
        };
    }

    private static bool IsMissingEvalRun(SharpOMaticException exception, Guid evalRunId)
    {
        return exception.Message == $"EvalRun '{evalRunId}' cannot be found.";
    }

    private static async Task<bool> EvalRunExists(IRepositoryService repository, Guid evalRunId)
    {
        try
        {
            await repository.GetEvalRun(evalRunId);
            return true;
        }
        catch (SharpOMaticException ex) when (IsMissingEvalRun(ex, evalRunId))
        {
            return false;
        }
    }

    private static List<EvalRow> ResolveEvalRowsForRun(List<EvalRow> allRows, int? sampleCount)
    {
        if (!sampleCount.HasValue)
            return allRows;

        if (allRows.Count == 0)
            throw new SharpOMaticException("Sample count cannot be provided when there are no rows to run.");

        var requestedSampleCount = sampleCount.Value;
        if (requestedSampleCount < 1 || requestedSampleCount > allRows.Count)
            throw new SharpOMaticException($"Sample count must be between 1 and {allRows.Count}.");

        if (requestedSampleCount == allRows.Count)
            return allRows;

        var selectedRowIds = BuildRandomSampleRowIds(allRows, requestedSampleCount);
        return allRows.Where(row => selectedRowIds.Contains(row.EvalRowId)).ToList();
    }

    private static HashSet<Guid> BuildRandomSampleRowIds(List<EvalRow> allRows, int requestedSampleCount)
    {
        var rowIds = allRows.Select(row => row.EvalRowId).ToArray();
        for (var index = rowIds.Length - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (rowIds[index], rowIds[swapIndex]) = (rowIds[swapIndex], rowIds[index]);
        }

        return rowIds.Take(requestedSampleCount).ToHashSet();
    }

    private static string ContextPath(EvalColumn evalColumn)
    {
        if (!string.IsNullOrWhiteSpace(evalColumn.InputPath))
            return evalColumn.InputPath;
        else
            return evalColumn.Name;
    }

    private static string ResolveEvalRunName(string? name, DateTime started)
    {
        var normalized = name?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
            return normalized;

        return started.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private async Task<NextNodeData> BuildFreshTurnStart(
        ProcessContext processContext,
        Guid workflowId,
        ConversationCheckpoint? previousCheckpoint,
        NodeResumeInput resumeInput,
        ContextEntryListEntity? inputEntries
    )
    {
        var rootWorkflow = await RepositoryService.GetWorkflow(workflowId);
        ValidateConversationWorkflow(rootWorkflow);
        processContext.PinWorkflowSnapshot(rootWorkflow);

        ContextObject nodeContext;
        if (!string.IsNullOrWhiteSpace(previousCheckpoint?.ContextJson))
            nodeContext = ContextObject.Deserialize(previousCheckpoint.ContextJson, processContext.JsonConverters);
        else
            nodeContext = [];

        ApplyConversationStartInput(nodeContext, resumeInput);
        processContext.Run.InputEntries = await ApplyInputEntries(processContext.ServiceScope.ServiceProvider, nodeContext, inputEntries, processContext.Run.RunId);

        processContext.Run.InputContext = nodeContext.Serialize(processContext.JsonConverters);
        var workflowContext = new WorkflowContext(processContext, rootWorkflow);
        var startNode = rootWorkflow.Nodes.Single(n => n.NodeType == NodeType.Start);
        var threadContext = processContext.CreateThread(nodeContext, workflowContext);
        return new NextNodeData(threadContext, startNode);
    }

    private async Task<NextNodeData> BuildSuspendedTurnStart(
        ProcessContext processContext,
        Guid workflowId,
        ConversationCheckpoint? checkpoint,
        NodeResumeInput resumeInput
    )
    {
        if (checkpoint is null)
            throw new SharpOMaticException("Conversation is suspended but has no checkpoint.");

        var workflowSnapshots = processContext.GetPinnedWorkflowSnapshots();
        var rootSnapshot = workflowSnapshots.FirstOrDefault(snapshot => snapshot.WorkflowId == workflowId)
            ?? throw new SharpOMaticException("Suspended conversation is missing the root workflow snapshot.");

        var rootWorkflow = WorkflowSnapshotSerializer.DeserializeWorkflow(rootSnapshot.WorkflowJson);
        var currentContext = (SharpOMatic.Engine.Contexts.ExecutionContext)new WorkflowContext(processContext, rootWorkflow);

        var gosubFrames = DeserializeGosubFrames(checkpoint.GosubStackJson);
        foreach (var frame in gosubFrames)
        {
            var parentWorkflowContext = currentContext.GetWorkflowContext();
            var returnNode = frame.ReturnNodeId.HasValue ? parentWorkflowContext.Workflow.Nodes.Single(n => n.Id == frame.ReturnNodeId.Value) : null;
            var parentContext = ContextObject.Deserialize(frame.ParentContextJson, processContext.JsonConverters);
            var outputMappings = JsonSerializer.Deserialize<ContextEntryListEntity>(frame.OutputMappingsJson)
                ?? new ContextEntryListEntity() { Id = Guid.NewGuid(), Version = 1, Entries = [] };

            var gosubContext = new GosubContext(currentContext, frame.ParentTraceId, parentContext, returnNode, frame.ApplyOutputMappings, outputMappings);
            gosubContext.IncrementThreads();

            var childSnapshot = workflowSnapshots.FirstOrDefault(snapshot => snapshot.WorkflowId == frame.ChildWorkflowId)
                ?? throw new SharpOMaticException($"Suspended conversation is missing workflow snapshot '{frame.ChildWorkflowId}'.");

            var childWorkflow = WorkflowSnapshotSerializer.DeserializeWorkflow(childSnapshot.WorkflowJson);
            var childWorkflowContext = new WorkflowContext(gosubContext, childWorkflow);
            gosubContext.ChildWorkflowContext = childWorkflowContext;
            currentContext = childWorkflowContext;
        }

        var nodeContext = ContextObject.Deserialize(checkpoint.ContextJson, processContext.JsonConverters);
        ApplyResumeInputContextEffects(nodeContext, resumeInput);

        processContext.Run.InputContext = nodeContext.Serialize(processContext.JsonConverters);
        var threadContext = processContext.RestoreThread(nodeContext, currentContext);
        var resumeNode = currentContext.GetWorkflowContext().Workflow.Nodes.Single(n => n.Id == checkpoint.ResumeNodeId);
        return new NextNodeData(threadContext, resumeNode, NodeInvocationKind.Resume, resumeInput);
    }

    private static IReadOnlyList<ConversationWorkflowSnapshot> DeserializeWorkflowSnapshots(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<ConversationWorkflowSnapshot>>(json) ?? [];
    }

    private static IReadOnlyList<ConversationGosubFrame> DeserializeGosubFrames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<ConversationGosubFrame>>(json) ?? [];
    }

    private static void ValidateConversationWorkflow(WorkflowEntity workflow)
    {
        ValidateWorkflowExecutionMode(workflow, allowConversation: true);
    }

    private static void ApplyConversationStartInput(ContextObject nodeContext, NodeResumeInput resumeInput)
    {
        switch (resumeInput)
        {
            case ContinueResumeInput:
                return;
            case AgUiAgentResumeInput agUiAgent:
                if (agUiAgent.Context is not null)
                    ContextHelpers.OverwriteContexts(nodeContext, agUiAgent.Context);

                nodeContext["agent"] = agUiAgent.Agent;
                return;
            case ContextMergeResumeInput contextMerge:
                ContextHelpers.OverwriteContexts(nodeContext, contextMerge.Context);
                return;
            default:
                throw new SharpOMaticException($"Conversation start cannot handle resume input type '{resumeInput.GetType().Name}'.");
        }
    }

    private static void ApplyResumeInputContextEffects(ContextObject nodeContext, NodeResumeInput resumeInput)
    {
        switch (resumeInput)
        {
            case ContinueResumeInput:
                return;
            case AgUiAgentResumeInput agUiAgent:
                if (agUiAgent.Context is not null)
                    ContextHelpers.OverwriteContexts(nodeContext, agUiAgent.Context);

                nodeContext["agent"] = agUiAgent.Agent;
                return;
            case ContextMergeResumeInput contextMerge:
                ContextHelpers.OverwriteContexts(nodeContext, contextMerge.Context);
                return;
            default:
                return;
        }
    }

}
