using SharpOMatic.Engine.Contexts;
using SharpOMatic.Engine.Entities;

namespace SharpOMatic.Engine.Services;

public class SharpOMaticEngine(ISharpOMaticRepository Repository, 
                               ISharpOMaticNotification Notifications,
                               IEnumerable<JsonConverter> JsonConverters)  
    : ISharpOMaticEngine
{
    private const int MAX_EXECUTION_NODES = 100;

    public async Task<Guid> RunWorkflow(Guid workflowId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null)
    {
        context ??= [];

        string? inputJson = null;
        if (inputEntries is not null)
        {
            inputJson = JsonSerializer.Serialize(inputEntries);

            foreach (var entry in inputEntries!.Entries)
            {
                var entryValue = await ContextHelpers.EvaluateContextEntryValue(context, entry);
                if (!context.TrySet(entry.InputPath, entryValue))
                    throw new SharpOMaticException($"Input entry '{entry.InputPath}' could not be assigned the value.");
            }
        }

        var run = new Run()
        {
            WorkflowId = workflowId,
            RunId = Guid.NewGuid(),
            RunStatus = RunStatus.Created,
            Message = "Created",
            Created = DateTime.Now,
            InputEntries = inputJson,
        };

        await RunUpdated(run);

        _ = Task.Run(() => BackgroundRunWorkflow(workflowId, context, run));

        return run.RunId;
    }

    private async Task BackgroundRunWorkflow(Guid workflowId, ContextObject nodeContext, Run run)
    {
        try
        {
            var workflow = await Repository.GetWorkflow(workflowId) ?? throw new SharpOMaticException($"Could not load workflow {workflowId}.");
            var currentNodes = workflow.Nodes.Where(n => n.NodeType == NodeType.Start).ToList();
            if (currentNodes.Count != 1)
                throw new SharpOMaticException("Must have exactly one start node.");

            nodeContext.Add("WorkflowId", workflowId);
            nodeContext.Add("RunId", run.RunId);

            var runContext = new RunContext(Repository, Notifications, JsonConverters, workflow, run.RunId, nodeContext);

            run.RunStatus = RunStatus.Running;
            run.Message = "Running";
            run.Started = DateTime.Now;
            run.InputContext = runContext.TypedSerialization(runContext.NodeContext);
            await RunUpdated(run);

            int runCount = 0;
            NodeEntity? lastEntity = null;

            var currentNode = currentNodes[0];
            while (true)
            {
                lastEntity = currentNode;
                var nextNode = await RunNode(runContext, currentNode);
                runCount++;

                if (nextNode is null)
                {
                    if ((lastEntity is null) || (lastEntity is not EndNodeEntity))
                        throw new SharpOMaticException("Must finish on an end node.");

                    break;
                }

                if (runCount >= MAX_EXECUTION_NODES)
                    throw new SharpOMaticException( $"Aborted because {MAX_EXECUTION_NODES} node execution limit was reached.");

                currentNode = nextNode;
            }

            run.RunStatus = RunStatus.Success;
            run.Message = "Success";
            run.Stopped = DateTime.Now;
            run.OutputContext = runContext.TypedSerialization(runContext.NodeContext); 
            await RunUpdated(run);
        }
        catch (Exception ex)
        {
            run.RunStatus = RunStatus.Failed;
            run.Message = "Failed";
            run.Stopped = DateTime.Now;
            run.Error = ex.Message;
            await RunUpdated(run);
        }
    }

    private static Task<NodeEntity?> RunNode(RunContext runContext, NodeEntity node)
    {
        return node switch
        {
            StartNodeEntity startNode => new StartNode(runContext, startNode).Run(),
            EndNodeEntity endNode => new EndNode(runContext, endNode).Run(),
            EditNodeEntity editNode => new EditNode(runContext, editNode).Run(),
            CodeNodeEntity codeNode => new CodeNode(runContext, codeNode).Run(),
            SwitchNodeEntity switchNode => new SwitchNode(runContext, switchNode).Run(),
            FanInNodeEntity fanInNode => new FanInNode(runContext, fanInNode).Run(),
            FanOutNodeEntity fanOutNode => new FanOutNode(runContext, fanOutNode).Run(),
            _ => throw new SharpOMaticException($"Unrecognized node type' {node.NodeType}'")
        };
    }

    private async Task RunUpdated(Run run)
    {
        await Repository.UpsertRun(run);
        await Notifications.RunProgress(run);
    }
}
