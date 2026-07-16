using System.Diagnostics;

namespace SharpOMatic.Engine.Services;

public static class SharpOMaticDiagnostics
{
    public const string SourceName = "SharpOMatic.Engine";

    internal static readonly ActivitySource ActivitySource = new(SourceName);

    internal static Activity? StartRunActivity(Run run, string? workflowName)
    {
        var activity = ActivitySource.StartActivity(BuildRunActivityName(workflowName));
        if (activity is null)
            return null;

        // The GenAI semantic-convention tags classify the run as an agent invocation so
        // backends like the Application Insights Agents view list it under Agent runs.
        activity.SetTag("gen_ai.operation.name", "invoke_agent");
        activity.SetTag("gen_ai.provider.name", "sharpomatic");
        activity.SetTag("gen_ai.agent.id", run.WorkflowId);
        activity.SetTag("workflow.id", run.WorkflowId);
        activity.SetTag("sharpomatic.run.id", run.RunId);

        if (workflowName is not null)
        {
            activity.SetTag("gen_ai.agent.name", workflowName);
            activity.SetTag("workflow.name", workflowName);
        }

        if (!string.IsNullOrWhiteSpace(run.ConversationId))
        {
            activity.SetTag("gen_ai.conversation.id", run.ConversationId);
            activity.SetTag("session.id", run.ConversationId);
            if (run.TurnNumber.HasValue)
                activity.SetTag("sharpomatic.conversation.turn_number", run.TurnNumber.Value);
        }

        return activity;
    }

    internal static void CompleteRunActivity(Activity? activity, Run run, WorkflowRunMetric? metric)
    {
        if (activity is null)
            return;

        if (metric is not null)
        {
            activity.DisplayName = BuildRunActivityName(metric.WorkflowName);
            activity.SetTag("gen_ai.agent.name", metric.WorkflowName);
            activity.SetTag("workflow.name", metric.WorkflowName);
            activity.SetTag("gen_ai.usage.input_tokens", metric.InputTokens);
            activity.SetTag("gen_ai.usage.output_tokens", metric.OutputTokens);
            activity.SetTag("sharpomatic.model_call.count", metric.ModelCallCount);
            activity.SetTag("sharpomatic.model_call.total_cost", (double)metric.TotalModelCost);

            if (metric.ErrorType is not null)
                activity.SetTag("error.type", metric.ErrorType);

            if (metric.FailedNodeEntityId.HasValue)
            {
                activity.SetTag("sharpomatic.failed_node.id", metric.FailedNodeEntityId.Value);
                activity.SetTag("sharpomatic.failed_node.title", metric.FailedNodeTitle);
            }
        }

        activity.SetTag("sharpomatic.run.status", run.RunStatus.ToString());

        if (run.RunStatus == RunStatus.Failed)
            activity.SetStatus(ActivityStatusCode.Error, string.IsNullOrWhiteSpace(run.Error) ? run.Message : run.Error);
        else
            activity.SetStatus(ActivityStatusCode.Ok);

        activity.Dispose();
    }

    internal static Activity? StartNodeActivity(ProcessContext processContext, NodeEntity node)
    {
        // Nodes execute on queue worker threads where Activity.Current does not flow from the
        // caller that started the run, so the run activity carried on ProcessContext is the parent.
        var runActivity = processContext.RunActivity;
        if (runActivity is null)
            return null;

        var activity = ActivitySource.StartActivity($"executor.process {node.Title}", ActivityKind.Internal, runActivity.Context);
        if (activity is null)
            return null;

        activity.SetTag("executor.id", node.Id);
        activity.SetTag("executor.type", node.NodeType.ToString());
        activity.SetTag("sharpomatic.run.id", processContext.Run.RunId);
        return activity;
    }

    internal static void CompleteNodeActivity(Activity? activity, NodeStatus nodeStatus, Exception? exception, string? error)
    {
        if (activity is null)
            return;

        activity.SetTag("sharpomatic.node.status", nodeStatus.ToString());

        if (nodeStatus == NodeStatus.Failed)
        {
            if (exception is not null)
            {
                activity.SetTag("error.type", exception.GetType().FullName);
                activity.AddException(exception);
            }

            activity.SetStatus(ActivityStatusCode.Error, error);
        }
        else
            activity.SetStatus(ActivityStatusCode.Ok);

        activity.Dispose();
    }

    private static string BuildRunActivityName(string? workflowName)
    {
        return string.IsNullOrWhiteSpace(workflowName) ? "invoke_agent" : $"invoke_agent {workflowName}";
    }
}
