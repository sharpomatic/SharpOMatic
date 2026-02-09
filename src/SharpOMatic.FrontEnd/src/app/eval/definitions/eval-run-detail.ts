import { EvalRunSummarySnapshot } from './eval-run-summary';

export interface EvalRunGraderSummaryDetailSnapshot {
  evalRunGraderSummaryId: string;
  evalRunId: string;
  evalGraderId: string;
  label: string;
  order: number;
  passThreshold: number | null;
  totalCount: number;
  completedCount: number;
  failedCount: number;
  minScore: number | null;
  maxScore: number | null;
  averageScore: number | null;
  medianScore: number | null;
  standardDeviation: number | null;
  passRate: number | null;
}

export interface EvalRunDetailSnapshot {
  evalRun: EvalRunSummarySnapshot;
  graderSummaries: EvalRunGraderSummaryDetailSnapshot[];
}
