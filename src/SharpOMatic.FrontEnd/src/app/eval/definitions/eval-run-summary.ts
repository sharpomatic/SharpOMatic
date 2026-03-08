import { EvalRunStatus } from '../enumerations/eval-run-status';

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
}
