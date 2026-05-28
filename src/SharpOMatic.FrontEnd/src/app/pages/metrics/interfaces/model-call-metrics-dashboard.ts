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
  recentCalls: ModelCallMetricCallSummary[];
  recentCallsTotal: number;
  slowestCalls: ModelCallMetricCallSummary[];
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
  errorType: string | null;
  errorMessage: string | null;
}
