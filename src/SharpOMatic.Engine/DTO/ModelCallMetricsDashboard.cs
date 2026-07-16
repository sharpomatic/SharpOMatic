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
    int RecentTake,
    bool AllTime = false
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
    List<ModelCallMetricFailureCategoryGroup> FailureCategories,
    List<ModelCallMetricCallSummary> RecentCalls,
    int RecentCallsTotal,
    List<ModelCallMetricCallSummary> SlowestCalls,
    List<ModelCallMetricLogicalCallSummary> RecentLogicalCalls,
    int RecentLogicalCallsTotal,
    List<ModelCallMetricLogicalCallSummary> SlowestLogicalCalls
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
    double FailureRate,
    int LogicalCalls,
    int CallsRequiringFallback,
    int RecoveredCalls,
    int UnrecoveredCalls,
    double FallbackRecoveryRate,
    double LogicalFailureRate
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
    long? P95Duration,
    int LogicalCalls,
    int PrimaryAttempts,
    int FallbackAttempts,
    int SuccessfulFallbackAttempts,
    int FailedFallbackAttempts,
    int RecoveredCalls,
    int UnrecoveredCalls
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
    double FailureRate,
    int PrimaryAttempts,
    int FallbackAttempts,
    int SuccessfulFallbackAttempts
);

public sealed record ModelCallMetricFailureCategoryGroup(
    ModelFallbackFailureCategory Category,
    int? ProviderStatusCode,
    int Count,
    DateTime LastSeen,
    int PrimaryAttempts,
    int FallbackAttempts
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
    Guid LogicalCallId,
    int AttemptNumber,
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
    ModelFallbackFailureCategory? FailureCategory,
    int? ProviderStatusCode,
    string? ErrorType,
    string? ErrorMessage
);

public sealed record ModelCallMetricLogicalCallSummary(
    Guid LogicalCallId,
    DateTime Created,
    string WorkflowName,
    string NodeTitle,
    string? FinalConnectorName,
    string? FinalModelName,
    int AttemptCount,
    long? Duration,
    long TotalTokens,
    decimal? TotalCost,
    bool Succeeded,
    bool RecoveredByFallback,
    List<ModelCallMetricCallSummary> Attempts
);
