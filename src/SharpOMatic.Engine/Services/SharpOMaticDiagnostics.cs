using System.Diagnostics;

namespace SharpOMatic.Engine.Services;

public static class SharpOMaticDiagnostics
{
    public const string SourceName = "SharpOMatic.Engine";

    internal static readonly ActivitySource ActivitySource = CreateActivitySource();

    private static readonly string[] AgentUsageTagsToStrip =
    [
        "gen_ai.usage.input_tokens",
        "gen_ai.usage.output_tokens",
        "gen_ai.usage.total_tokens",
    ];

    /// <summary>
    /// Forces this type's initialization, which installs the listener that strips the duplicate usage tags
    /// from agent activities. Callers that only read <see cref="SourceName"/> would otherwise never touch
    /// the type, because the compiler inlines that const and no static initialization is triggered.
    /// </summary>
    internal static void EnsureListenerInstalled() => _ = ActivitySource;

    private static ActivitySource CreateActivitySource()
    {
        // Registered here rather than in a static constructor because SourceName is a const the compiler
        // inlines, so reading it never triggers a static constructor and the listener would not be
        // installed until something else happened to touch this type.
        ActivitySource.AddActivityListener(
            new ActivityListener
            {
                ShouldListenTo = source => source.Name == SourceName,
                // Sample nothing on our own account: this listener exists only to edit activities the
                // host's trace provider has already decided to record.
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.None,
                ActivityStopped = StripDuplicateAgentUsage,
            }
        );

        return new ActivitySource(SourceName);
    }

    /// <summary>
    /// Removes the token usage the Agent Framework middleware records on its <c>invoke_agent</c> activity.
    /// That activity spans exactly the <c>chat</c> activities nested underneath it, which record the usage
    /// of each provider round trip, so counting both totals the same tokens twice. The agent activity is
    /// also the one without a model name to group by, so the duplicate lands in an unattributed bucket
    /// rather than showing up as an obvious error.
    ///
    /// The chat activities win because they are the billing truth: they cover every provider round trip,
    /// including the attempts a retry or fallback later discarded. The agent activity keeps everything
    /// else - its duration, identity and turn structure are all still useful.
    /// </summary>
    private static void StripDuplicateAgentUsage(Activity activity)
    {
        // The agent middleware reuses the "chat" operation name for its own activity, so the operation
        // name cannot tell the two apart; gen_ai.operation.name is what distinguishes them.
        if (activity.GetTagItem("gen_ai.operation.name")?.ToString() != "invoke_agent")
            return;

        foreach (var tag in AgentUsageTagsToStrip)
            activity.SetTag(tag, null);
    }

    internal static Activity? StartRunActivity(Run run, string? workflowName)
    {
        var activity = ActivitySource.StartActivity(BuildRunActivityName(workflowName));
        if (activity is null)
            return null;

        // A run executes a statically authored graph, so it is not a GenAI agent invocation:
        // control flow comes from the workflow definition rather than from a model. The GenAI
        // spans belong to the model calls nested underneath it.
        activity.SetTag("sharpomatic.workflow.id", run.WorkflowId);
        activity.SetTag("sharpomatic.run.id", run.RunId);

        if (workflowName is not null)
            activity.SetTag("sharpomatic.workflow.name", workflowName);

        if (!string.IsNullOrWhiteSpace(run.ConversationId))
        {
            // Deliberately unprefixed: these two are OpenTelemetry semantic conventions rather than
            // SharpOMatic attributes, and backends key their conversation and session grouping off
            // these exact names. Everything SharpOMatic defines itself carries the sharpomatic prefix.
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
            activity.SetTag("sharpomatic.workflow.name", metric.WorkflowName);
            activity.SetTag("sharpomatic.usage.input_tokens", metric.InputTokens);
            activity.SetTag("sharpomatic.usage.output_tokens", metric.OutputTokens);
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

        activity.SetTag("sharpomatic.executor.id", node.Id);
        activity.SetTag("sharpomatic.executor.type", node.NodeType.ToString());
        activity.SetTag("sharpomatic.executor.title", node.Title);
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
        return string.IsNullOrWhiteSpace(workflowName) ? "workflow" : $"workflow {workflowName}";
    }
}
