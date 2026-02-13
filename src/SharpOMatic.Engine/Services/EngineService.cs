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

    public async Task<EvalRun> StartEvalRun(Guid workflowId)
    {
        return await StartEvalRunInternal(workflowId);
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

    private async Task<EvalRun> StartEvalRunInternal(Guid evalConfigId)
    {
        var evalConfigDetail = await RepositoryService.GetEvalConfigDetail(evalConfigId);

        var evalRun = new EvalRun
        {
            EvalRunId = Guid.NewGuid(),
            EvalConfigId = evalConfigId,
            Started = DateTime.Now,
            Finished = null,
            Status = EvalRunStatus.Running,
            Message = "Starting",
            Error = null,
            TotalRows = evalConfigDetail.Rows.Count,
            CompletedRows = 0,
            FailedRows = 0,
            CanceledRows = 0,
        };

        await RepositoryService.UpsertEvalRun(evalRun);
        _ = Task.Run(() => PerformEvalRun(evalConfigDetail, evalRun));

        return evalRun;
    }

    private async Task PerformEvalRun(EvalConfigDetail evalConfigDetail, EvalRun evalRun)
    {
        using var serviceScope = ScopeFactory.CreateScope();
        var provider = serviceScope.ServiceProvider;
        var repository = provider.GetRequiredService<IRepositoryService>();
        var jsonConverterService = provider.GetRequiredService<IJsonConverterService>();

        try
        {
            foreach(var row in evalConfigDetail.Rows.OrderBy(r => r.Order))
            {
                var evalRunRow = await PerformEvalRow(repository, jsonConverterService, evalRun.EvalRunId, evalConfigDetail, row);
                if (evalRunRow.Status == EvalRunStatus.Completed)
                    evalRun.CompletedRows++;
                else
                    evalRun.FailedRows++;
            }

            foreach (var evalGrader in evalConfigDetail.Graders.OrderBy(g => g.Order))
            {
                // TODO
                // Get all the EvalRunRowGrader entries for this grader
                // Calculate all the metrics
                // Create EvalRunGraderSummary and upsert
            }

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
        IRepositoryService repository,
        IJsonConverterService jsonConverterService,
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
                            if (columnData.StringValue is not null)
                            {
                                try
                                {
                                    var obj = ContextHelpers.FastDeserializeString(columnData.StringValue);
                                    inputContext.Set(ContextPath(column), obj);
                                }
                                catch
                                {
                                    throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' could not be parsed as json.");
                                }
                            }
                            else if (!column.Optional)
                                throw new SharpOMaticException($"Mandatory column '{column.Name}' for row '{rowName}' has missing json value.");
                            break;
                        case ContextEntryType.Expression:
                            // TODO
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
            }

            var runId = await CreateWorkflowRun(evalConfigDetail.EvalConfig.WorkflowId ?? Guid.Empty);
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
            await repository.UpsertEvalRunRows(evalRunId, [evalRunRow]);
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

                    if (graderOutput.TryGet<string>("payload", out var payload))
                        evalRunRowGrader.Payload = payload;
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
            await repository.UpsertEvalRunRowGraders(evalRunId, [evalRunRowGrader]);
        }

    }

    private static string ContextPath(EvalColumn evalColumn)
    {
        if (!string.IsNullOrWhiteSpace(evalColumn.InputPath))
            return evalColumn.InputPath;
        else
            return evalColumn.Name;
    }
}
