import { EvalRunStatus } from '../enumerations/eval-run-status';

export interface EvalRunRowGraderDetailSnapshot {
  evalRunRowGraderId: string;
  evalRunRowId: string;
  evalGraderId: string;
  label: string;
  order: number;
  status: EvalRunStatus;
  started: string;
  finished: string | null;
  score: number | null;
  payload: string | null;
  error: string | null;
}

export interface EvalRunRowDetailSnapshot {
  evalRunRowId: string;
  evalRunId: string;
  evalRowId: string;
  name: string;
  order: number;
  status: EvalRunStatus;
  started: string;
  finished: string | null;
  outputContext: string | null;
  error: string | null;
  graders: EvalRunRowGraderDetailSnapshot[];
}
