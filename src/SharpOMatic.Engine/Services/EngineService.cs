using SharpOMatic.Engine.Interfaces;

namespace SharpOMatic.Engine.Services;

public class EngineService(INodeQueueService QueueService,
                           IRepositoryService RepositoryService,
                           IRunContextFactory RunContextFactory,
                           IScriptOptionsService ScriptOptionsService,
                           IJsonConverterService JsonConverterService) : IEngineService
{
    public async Task<Guid> TryGetWorkflowId(string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new SharpOMaticException("Workflow name cannot be empty or whitespace.");

        var summaries = await RepositoryService.GetWorkflowEditSummaries();
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
            InputContext = JsonSerializer.Serialize(inputContext, new JsonSerializerOptions().BuildOptions(converters))
        };

        await RepositoryService.UpsertRun(run);
        return run;
    }

    private async Task StartRunInternal(
        Run run,
        ContextObject? nodeContext,
        ContextEntryListEntity? inputEntries,
        TaskCompletionSource<Run>? completionSource)
    {
        nodeContext ??= [];

        var inputJson = await ApplyInputEntries(nodeContext, inputEntries);

        var workflow = await RepositoryService.GetWorkflow(run.WorkflowId) ?? throw new SharpOMaticException($"Could not load workflow {run.WorkflowId}.");
        var currentNodes = workflow.Nodes.Where(n => n.NodeType == NodeType.Start).ToList();
        if (currentNodes.Count != 1)
            throw new SharpOMaticException("Must have exactly one start node.");

        var converters = JsonConverterService.GetConverters();
        run.InputEntries = inputJson;
        run.InputContext = JsonSerializer.Serialize(nodeContext, new JsonSerializerOptions().BuildOptions(converters));

        var nodeRunLimitSetting = await RepositoryService.GetSetting("RunNodeLimit");
        var nodeRunLimit = nodeRunLimitSetting?.ValueInteger ?? NodeExecutionService.DEFAULT_NODE_RUN_LIMIT;

        var runContext = RunContextFactory.Create(workflow, run, converters, nodeRunLimit, completionSource);
        var threadContext = new ThreadContext(runContext, nodeContext);

        await runContext.RunUpdated();
        QueueService.Enqueue(threadContext, currentNodes[0]);
    }

    private async Task<string?> ApplyInputEntries(ContextObject nodeContext, ContextEntryListEntity? inputEntries)
    {
        if (inputEntries is null)
            return null;

        var inputJson = JsonSerializer.Serialize(inputEntries);
        foreach (var entry in inputEntries.Entries)
        {
            var entryValue = await ContextHelpers.ResolveContextEntryValue(nodeContext, entry, ScriptOptionsService);
            if (!nodeContext.TrySet(entry.InputPath, entryValue))
                throw new SharpOMaticException($"Input entry '{entry.InputPath}' could not be assigned the value.");
        }

        return inputJson;
    }
}
