using Google.GenAI.Types;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using SharpOMatic.Engine.DTO;
using SharpOMatic.Engine.Entities.Definitions;
using SharpOMatic.Engine.Interfaces;
using SharpOMatic.Engine.Repository;
using SQLitePCL;
using System;
using System.Net.Http.Headers;

namespace SharpOMatic.Engine.Services;

public class EngineService(
    IServiceScopeFactory ScopeFactory,
    INodeQueueService QueueService,
    IRepositoryService RepositoryService,
    IScriptOptionsService ScriptOptionsService,
    IJsonConverterService JsonConverterService
) : IEngineService
{
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

    public async Task<Guid> CreateWorkflowRun(Guid workflowId)
    {
        var run = await CreateRunInternal(workflowId);
        return run.RunId;
    }

    public async Task<Run> StartWorkflowRunAndWait(Guid runId, ContextObject? nodeContext = null, ContextEntryListEntity? inputEntries = null)
    {
        var run = await RepositoryService.GetRun(runId);
        if (run is null)
            throw new SharpOMaticException($"Run '{runId}' cannot be found.");

        if (run.RunStatus != RunStatus.Created)
            throw new SharpOMaticException($"Run '{runId}' is not in a Created state.");

        var completionSource = new TaskCompletionSource<Run>(TaskCreationOptions.RunContinuationsAsynchronously);
        await StartRunInternal(run, nodeContext, inputEntries, completionSource);
        return await completionSource.Task.ConfigureAwait(false);
    }

    public async Task StartWorkflowRunAndNotify(Guid runId, ContextObject? nodeContext = null, ContextEntryListEntity? inputEntries = null)
    {
        var run = await RepositoryService.GetRun(runId);
        if (run is null)
            throw new SharpOMaticException($"Run '{runId}' cannot be found.");

        if (run.RunStatus != RunStatus.Created)
            throw new SharpOMaticException($"Run '{runId}' is not in a Created state.");

        var completionSource = new TaskCompletionSource<Run>(TaskCreationOptions.RunContinuationsAsynchronously);
        await StartRunInternal(run, nodeContext, inputEntries, completionSource);
    }

    public Guid CreateWorkflowRunSynchronously(Guid workflowId)
    {
        return CreateWorkflowRun(workflowId).GetAwaiter().GetResult();
    }

    public Run StartWorkflowRunSynchronously(Guid runId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null)
    {
        return StartWorkflowRunAndWait(runId, context, inputEntries).GetAwaiter().GetResult();
    }

    public async Task<EvalRun> StartEvalRun(Guid evalConfigId, string? name = null, int? sampleCount = null)
    {
        return await StartEvalRunInternal(evalConfigId, name, sampleCount);
    }

    private async Task<Run> CreateRunInternal(Guid workflowId)
    {
        var workflow = await RepositoryService.GetWorkflow(workflowId);
        if (workflow is null)
            throw new SharpOMaticException($"Could not load workflow {workflowId}.");

        if (workflow.Nodes.Count(n => n.NodeType == NodeType.Start) != 1)
            throw new SharpOMaticException("Must have exactly one start node.");

        var converters = JsonConverterService.GetConverters();
        var inputContext = new ContextObject();

        var run = new Run()
        {
            WorkflowId = workflowId,
            RunId = Guid.NewGuid(),
            RunStatus = RunStatus.Created,
            Message = "Created",
            Created = DateTime.Now,
            InputContext = JsonSerializer.Serialize(inputContext, new JsonSerializerOptions().BuildOptions(converters)),
        };

        await RepositoryService.UpsertRun(run);
        return run;
    }

    private async Task StartRunInternal(Run run, ContextObject? nodeContext, ContextEntryListEntity? inputEntries, TaskCompletionSource<Run>? completionSource)
    {
        nodeContext ??= [];

        var serviceScope = ScopeFactory.CreateScope();
        try
        {
            var inputJson = await ApplyInputEntries(serviceScope.ServiceProvider, nodeContext, inputEntries, run.RunId);

            var workflow = await RepositoryService.GetWorkflow(run.WorkflowId) ?? throw new SharpOMaticException($"Could not load workflow {run.WorkflowId}.");
            var currentNodes = workflow.Nodes.Where(n => n.NodeType == NodeType.Start).ToList();
            if (currentNodes.Count != 1)
                throw new SharpOMaticException("Must have exactly one start node.");

            var converters = JsonConverterService.GetConverters();
            run.InputEntries = inputJson;
            run.InputContext = JsonSerializer.Serialize(nodeContext, new JsonSerializerOptions().BuildOptions(converters));

            var nodeRunLimitSetting = await RepositoryService.GetSetting("RunNodeLimit");
            var nodeRunLimit = nodeRunLimitSetting?.ValueInteger ?? NodeExecutionService.DEFAULT_NODE_RUN_LIMIT;

            var processContext = new ProcessContext(serviceScope, run, nodeRunLimit, completionSource);
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
            Started = started,
            Finished = null,
            Status = EvalRunStatus.Running,
            Message = "Starting",
            Error = null,
            TotalRows = selectedRows.Count,
            CompletedRows = 0,
            FailedRows = 0,
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

        try
        {
            foreach (var row in selectedRows)
            {
                var evalRunRow = await PerformEvalRow(provider, repository, jsonConverterService, assetStore, scriptOptionsService, evalRun.EvalRunId, evalConfigDetail, row);
                if (evalRunRow.Status == EvalRunStatus.Completed)
                    evalRun.CompletedRows++;
                else
                    evalRun.FailedRows++;
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

                graderSummaries.Add(new EvalRunGraderSummary
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
                });
            }

            await repository.UpsertEvalRunGraderSummaries(graderSummaries);

            evalRun.Message = "Completed";
            evalRun.Status = EvalRunStatus.Completed;
        }
        catch (Exception ex)
        {
            evalRun.Message = "Failed";
            evalRun.Error = ex.Message;
            evalRun.Status = EvalRunStatus.Failed;
        }
        finally
        {
            evalRun.Finished = DateTime.Now;
            await repository.UpsertEvalRun(evalRun);
        }
    }

    private async Task<EvalRunRow> PerformEvalRow(
        IServiceProvider serviceProvider,
        IRepositoryService repository,
        IJsonConverterService jsonConverterService,
        IAssetStore assetStore,
        IScriptOptionsService scriptOptionsService,
        Guid evalRunId, 
        EvalConfigDetail evalConfigDetail, 
        EvalRow evalRow)
    {
        var evalRunRow = new EvalRunRow
        {
            EvalRunRowId = Guid.NewGuid(),
            EvalRunId = evalRunId,
            EvalRowId = evalRow.EvalRowId,
            Started = DateTime.Now,
            Status = EvalRunStatus.Running
        };

        await repository.UpsertEvalRunRows([evalRunRow]);

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
            var runId = await CreateWorkflowRun(evalConfigDetail.EvalConfig.WorkflowId ?? Guid.Empty);

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
                                    Assets = new AssetHelper(repository, assetStore, runId),
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

            var runResult = await StartWorkflowRunAndWait(runId, inputContext);
            
            foreach(var evalGrader in evalConfigDetail.Graders.OrderBy(g => g.Order))
                await PerformEvalGrader(repository, jsonConverterService, evalRunId, evalRunRow.EvalRunRowId, evalGrader, runResult.OutputContext);
            
            evalRunRow.OutputContext = runResult.OutputContext;
            evalRunRow.Status = EvalRunStatus.Completed;
        }
        catch (Exception ex)
        {
            evalRunRow.Error = ex.Message;
            evalRunRow.Status = EvalRunStatus.Failed;
        }
        finally
        {
            evalRunRow.Finished = DateTime.Now;
            await repository.UpsertEvalRunRows([evalRunRow]);
        }

        return evalRunRow;
    }

    private async Task PerformEvalGrader(
        IRepositoryService repository,
        IJsonConverterService jsonConverterService,
        Guid evalRunId,
        Guid evalRunRowId,
        EvalGrader evalGrader, 
        string? jsonContext)
    {
        var evalRunRowGrader = new EvalRunRowGrader
        {
            EvalRunRowGraderId = Guid.NewGuid(),
            EvalRunRowId = evalRunRowId,
            EvalGraderId = evalGrader.EvalGraderId,
            EvalRunId = evalRunId,
            Started = DateTime.Now,
            Status = EvalRunStatus.Running
        };

        try
        {
            ContextObject inputContext = [];
            if (!string.IsNullOrWhiteSpace(jsonContext))
                inputContext = ContextObject.Deserialize(jsonContext, jsonConverterService);

            var runId = await CreateWorkflowRun(evalGrader.WorkflowId ?? Guid.Empty);
            var runResult = await StartWorkflowRunAndWait(runId, inputContext);
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
                    if (graderOutput.TryGet<double>("score", out var score))
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
}
