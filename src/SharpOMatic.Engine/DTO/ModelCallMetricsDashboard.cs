namespace SharpOMatic.Engine.DTO;

public enum ModelCallMetricScope
{
    All = 0,
    Workflow = 1,
    Connector = 2,
    Model = 3,
}

public enum ModelCallMetricBucket
{
    Hour = 0,
    Day = 1,
    Week = 2,
}

public sealed record ModelCallMetricsDashboardRequest(
    DateTime Start,
    DateTime End,
    ModelCallMetricBucket Bucket,
    ModelCallMetricScope Scope,
    string? ScopeKey,
    string? MasterSearch,
    int RecentSkip,
    int RecentTake
);

public sealed record ModelCallMetricsDashboard(
    DateTime Start,
    DateTime End,
    ModelCallMetricBucket Bucket,
    ModelCallMetricScope Scope,
    string? ScopeKey,
    string? ScopeName,
    ModelCallMetricTotals Totals,
    List<ModelCallMetricMasterItem> MasterItems,
    List<ModelCallMetricTimeBucket> TimeBuckets,
    List<ModelCallMetricBreakdownItem> WorkflowBreakdown,
    List<ModelCallMetricBreakdownItem> ConnectorBreakdown,
    List<ModelCallMetricBreakdownItem> ModelBreakdown,
    List<ModelCallMetricBreakdownItem> NodeBreakdown,
    List<ModelCallMetricFailureGroup> Failures,
    List<ModelCallMetricCallSummary> RecentCalls,
    int RecentCallsTotal,
    List<ModelCallMetricCallSummary> SlowestCalls
);

public sealed record ModelCallMetricTotals(
    int TotalCalls,
    int SuccessfulCalls,
    int FailedCalls,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal TotalCost,
    int PricedCalls,
    int UnpricedCalls,
    double? AverageDuration,
    long? P95Duration,
    double FailureRate
);

public sealed record ModelCallMetricMasterItem(
    string Key,
    string Name,
    int TotalCalls,
    int FailedCalls,
    decimal TotalCost,
    long TotalTokens,
    double? AverageDuration,
    long? P95Duration,
    double FailureRate
);

public sealed record ModelCallMetricTimeBucket(
    DateTime Start,
    int TotalCalls,
    int SuccessfulCalls,
    int FailedCalls,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal TotalCost,
    int PricedCalls,
    int UnpricedCalls,
    double? AverageDuration,
    long? P95Duration
);

public sealed record ModelCallMetricBreakdownItem(
    string Key,
    string Name,
    int TotalCalls,
    int FailedCalls,
    decimal TotalCost,
    long TotalTokens,
    double? AverageDuration,
    long? P95Duration,
    double FailureRate
);

public sealed record ModelCallMetricFailureGroup(
    string ErrorType,
    string ErrorMessage,
    int Count,
    DateTime FirstSeen,
    DateTime LastSeen,
    int WorkflowCount,
    int ConnectorCount,
    int ModelCount
);

public sealed record ModelCallMetricCallSummary(
    Guid Id,
    DateTime Created,
    string WorkflowName,
    string NodeTitle,
    string? ConnectorName,
    string? ModelName,
    long? Duration,
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens,
    decimal? TotalCost,
    bool Succeeded,
    string? ErrorType,
    string? ErrorMessage
);
