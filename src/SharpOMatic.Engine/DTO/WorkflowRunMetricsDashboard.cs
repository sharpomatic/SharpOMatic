namespace SharpOMatic.Engine.DTO;

public enum WorkflowRunMetricScope
{
    All = 0,
    Workflow = 1,
    Error = 2,
}

public sealed record WorkflowRunMetricsDashboardRequest(
    DateTime Start,
    DateTime End,
    ModelCallMetricBucket Bucket,
    WorkflowRunMetricScope Scope,
    string? ScopeKey,
    string? MasterSearch,
    int RecentSkip,
    int RecentTake,
    bool AllTime = false
);

public sealed record WorkflowRunMetricsDashboard(
    DateTime Start,
    DateTime End,
    ModelCallMetricBucket Bucket,
    WorkflowRunMetricScope Scope,
    string? ScopeKey,
    string? ScopeName,
    WorkflowRunMetricTotals Totals,
    List<WorkflowRunMetricMasterItem> MasterItems,
    List<WorkflowRunMetricTimeBucket> TimeBuckets,
    List<WorkflowRunMetricBreakdownItem> WorkflowBreakdown,
    List<WorkflowRunMetricFailureGroup> Failures,
    List<WorkflowRunMetricRunSummary> RecentRuns,
    int RecentRunsTotal,
    List<WorkflowRunMetricRunSummary> SlowestRuns
);

public sealed record WorkflowRunMetricTotals(
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    int SuspendedRuns,
    double? AverageDuration,
    long? P95Duration,
    double FailureRate,
    int ModelCallCount,
    int ModelCallFailureCount,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal TotalModelCost
);

public sealed record WorkflowRunMetricMasterItem(
    string Key,
    string Name,
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    int SuspendedRuns,
    double? AverageDuration,
    long? P95Duration,
    double FailureRate,
    int ModelCallCount,
    int ModelCallFailureCount,
    long TotalTokens,
    decimal TotalModelCost
);

public sealed record WorkflowRunMetricTimeBucket(
    DateTime Start,
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    int SuspendedRuns,
    double? AverageDuration,
    long? P95Duration,
    int ModelCallCount,
    int ModelCallFailureCount,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal TotalModelCost
);

public sealed record WorkflowRunMetricBreakdownItem(
    string Key,
    string Name,
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    int SuspendedRuns,
    double? AverageDuration,
    long? P95Duration,
    double FailureRate,
    int ModelCallCount,
    int ModelCallFailureCount,
    long TotalTokens,
    decimal TotalModelCost
);

public sealed record WorkflowRunMetricFailureGroup(
    string ErrorType,
    string ErrorMessage,
    Guid? FailedNodeEntityId,
    string FailedNodeTitle,
    NodeType? FailedNodeType,
    int Count,
    DateTime FirstSeen,
    DateTime LastSeen,
    int WorkflowCount
);

public sealed record WorkflowRunMetricRunSummary(
    Guid Id,
    Guid RunId,
    DateTime Created,
    DateTime Started,
    DateTime Finished,
    long? Duration,
    Guid WorkflowId,
    string WorkflowName,
    int WorkflowVersion,
    bool Succeeded,
    RunStatus RunStatus,
    string? ErrorType,
    string? ErrorMessage,
    Guid? FailedNodeEntityId,
    string? FailedNodeTitle,
    NodeType? FailedNodeType,
    string? ConversationId,
    int? TurnNumber,
    bool IsConversationRun,
    int ModelCallCount,
    int ModelCallFailureCount,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal TotalModelCost
);
