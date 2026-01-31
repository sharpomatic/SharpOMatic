import { EvalConfigSnapshot } from './eval-config';
import { EvalGraderSnapshot } from './eval-grader';
import { EvalColumnSnapshot } from './eval-column';
import { EvalRowSnapshot } from './eval-row';
import { EvalDataSnapshot } from './eval-data';

export interface EvalConfigDetailSnapshot {
  evalConfig: EvalConfigSnapshot;
  graders: EvalGraderSnapshot[];
  columns: EvalColumnSnapshot[];
  rows: EvalRowSnapshot[];
  data: EvalDataSnapshot[];
}
