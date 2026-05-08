import { RunStatus } from '../../../enumerations/run-status';
import { NodeType } from '../../../entities/enumerations/node-type';
import { ModelCallMetricBucket } from './model-call-metrics-dashboard';

export enum WorkflowRunMetricScope {
  All = 0,
  Workflow = 1,
  Error = 2,
}

export interface WorkflowRunMetricsDashboard {
  start: string;
  end: string;
  bucket: ModelCallMetricBucket;
  scope: WorkflowRunMetricScope;
  scopeKey: string | null;
  scopeName: string | null;
  totals: WorkflowRunMetricTotals;
  masterItems: WorkflowRunMetricMasterItem[];
  timeBuckets: WorkflowRunMetricTimeBucket[];
  workflowBreakdown: WorkflowRunMetricBreakdownItem[];
  failures: WorkflowRunMetricFailureGroup[];
  recentRuns: WorkflowRunMetricRunSummary[];
  recentRunsTotal: number;
  slowestRuns: WorkflowRunMetricRunSummary[];
}

export interface WorkflowRunMetricTotals {
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  suspendedRuns: number;
  averageDuration: number | null;
  p95Duration: number | null;
  failureRate: number;
  modelCallCount: number;
  modelCallFailureCount: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  totalModelCost: number;
}

export interface WorkflowRunMetricMasterItem {
  key: string;
  name: string;
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  suspendedRuns: number;
  averageDuration: number | null;
  p95Duration: number | null;
  failureRate: number;
  modelCallCount: number;
  modelCallFailureCount: number;
  totalTokens: number;
  totalModelCost: number;
}

export interface WorkflowRunMetricTimeBucket {
  start: string;
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  suspendedRuns: number;
  averageDuration: number | null;
  p95Duration: number | null;
  modelCallCount: number;
  modelCallFailureCount: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  totalModelCost: number;
}

export interface WorkflowRunMetricBreakdownItem {
  key: string;
  name: string;
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  suspendedRuns: number;
  averageDuration: number | null;
  p95Duration: number | null;
  failureRate: number;
  modelCallCount: number;
  modelCallFailureCount: number;
  totalTokens: number;
  totalModelCost: number;
}

export interface WorkflowRunMetricFailureGroup {
  errorType: string;
  errorMessage: string;
  failedNodeEntityId: string | null;
  failedNodeTitle: string;
  failedNodeType: NodeType | null;
  count: number;
  firstSeen: string;
  lastSeen: string;
  workflowCount: number;
}

export interface WorkflowRunMetricRunSummary {
  id: string;
  runId: string;
  created: string;
  started: string;
  finished: string;
  duration: number | null;
  workflowId: string;
  workflowName: string;
  workflowVersion: number;
  succeeded: boolean;
  runStatus: RunStatus;
  errorType: string | null;
  errorMessage: string | null;
  failedNodeEntityId: string | null;
  failedNodeTitle: string | null;
  failedNodeType: NodeType | null;
  conversationId: string | null;
  turnNumber: number | null;
  isConversationRun: boolean;
  modelCallCount: number;
  modelCallFailureCount: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  totalModelCost: number;
}
