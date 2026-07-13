export enum ModelCallMetricScope {
  All = 0,
  Workflow = 1,
  Connector = 2,
  Model = 3,
}

export enum ModelCallMetricBucket {
  Hour = 0,
  Day = 1,
  Week = 2,
}

export interface ModelCallMetricsDashboard {
  start: string;
  end: string;
  bucket: ModelCallMetricBucket;
  scope: ModelCallMetricScope;
  scopeKey: string | null;
  scopeName: string | null;
  totals: ModelCallMetricTotals;
  masterItems: ModelCallMetricMasterItem[];
  timeBuckets: ModelCallMetricTimeBucket[];
  workflowBreakdown: ModelCallMetricBreakdownItem[];
  connectorBreakdown: ModelCallMetricBreakdownItem[];
  modelBreakdown: ModelCallMetricBreakdownItem[];
  nodeBreakdown: ModelCallMetricBreakdownItem[];
  failures: ModelCallMetricFailureGroup[];
  failureCategories: ModelCallMetricFailureCategoryGroup[];
  recentCalls: ModelCallMetricCallSummary[];
  recentCallsTotal: number;
  slowestCalls: ModelCallMetricCallSummary[];
  recentLogicalCalls: ModelCallMetricLogicalCallSummary[];
  recentLogicalCallsTotal: number;
  slowestLogicalCalls: ModelCallMetricLogicalCallSummary[];
}

export interface ModelCallMetricTotals {
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  totalCost: number;
  pricedCalls: number;
  unpricedCalls: number;
  averageDuration: number | null;
  p95Duration: number | null;
  failureRate: number;
  logicalCalls: number;
  callsRequiringFallback: number;
  recoveredCalls: number;
  unrecoveredCalls: number;
  fallbackRecoveryRate: number;
  logicalFailureRate: number;
}

export interface ModelCallMetricMasterItem {
  key: string;
  name: string;
  totalCalls: number;
  failedCalls: number;
  totalCost: number;
  totalTokens: number;
  averageDuration: number | null;
  p95Duration: number | null;
  failureRate: number;
}

export interface ModelCallMetricTimeBucket {
  start: string;
  totalCalls: number;
  successfulCalls: number;
  failedCalls: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  totalCost: number;
  pricedCalls: number;
  unpricedCalls: number;
  averageDuration: number | null;
  p95Duration: number | null;
  logicalCalls: number;
  primaryAttempts: number;
  fallbackAttempts: number;
  successfulFallbackAttempts: number;
  failedFallbackAttempts: number;
  recoveredCalls: number;
  unrecoveredCalls: number;
}

export interface ModelCallMetricBreakdownItem {
  key: string;
  name: string;
  totalCalls: number;
  failedCalls: number;
  totalCost: number;
  totalTokens: number;
  averageDuration: number | null;
  p95Duration: number | null;
  failureRate: number;
  primaryAttempts: number;
  fallbackAttempts: number;
  successfulFallbackAttempts: number;
}

export interface ModelCallMetricFailureCategoryGroup {
  category: ModelFallbackFailureCategory;
  providerStatusCode: number | null;
  count: number;
  lastSeen: string;
  primaryAttempts: number;
  fallbackAttempts: number;
}

export enum ModelFallbackFailureCategory {
  Unknown = 0,
  RateLimited = 1,
  ProviderUnavailable = 2,
  Timeout = 3,
  Network = 4,
  Authentication = 5,
  InvalidRequest = 6,
  Configuration = 7,
  Cancellation = 8,
}

export interface ModelCallMetricFailureGroup {
  errorType: string;
  errorMessage: string;
  count: number;
  firstSeen: string;
  lastSeen: string;
  workflowCount: number;
  connectorCount: number;
  modelCount: number;
}

export interface ModelCallMetricCallSummary {
  id: string;
  logicalCallId: string;
  attemptNumber: number;
  created: string;
  workflowName: string;
  nodeTitle: string;
  connectorName: string | null;
  modelName: string | null;
  duration: number | null;
  inputTokens: number | null;
  outputTokens: number | null;
  totalTokens: number | null;
  totalCost: number | null;
  succeeded: boolean;
  failureCategory: number | null;
  providerStatusCode: number | null;
  errorType: string | null;
  errorMessage: string | null;
}

export interface ModelCallMetricLogicalCallSummary {
  logicalCallId: string;
  created: string;
  workflowName: string;
  nodeTitle: string;
  finalConnectorName: string | null;
  finalModelName: string | null;
  attemptCount: number;
  duration: number | null;
  totalTokens: number;
  totalCost: number | null;
  succeeded: boolean;
  recoveredByFallback: boolean;
  attempts: ModelCallMetricCallSummary[];
}
