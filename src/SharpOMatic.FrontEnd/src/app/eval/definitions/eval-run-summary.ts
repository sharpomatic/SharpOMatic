import { EvalRunStatus } from '../enumerations/eval-run-status';
import { EvalRunScoreMode } from '../enumerations/eval-run-score-mode';

export interface EvalRunSummarySnapshot {
  evalRunId: string;
  evalConfigId: string;
  name: string;
  order: number;
  started: string;
  finished: string | null;
  status: EvalRunStatus;
  message: string | null;
  error: string | null;
  totalRows: number;
  completedRows: number;
  failedRows: number;
  averagePassRate: number | null;
  runScoreMode: EvalRunScoreMode;
  score: number | null;
}
