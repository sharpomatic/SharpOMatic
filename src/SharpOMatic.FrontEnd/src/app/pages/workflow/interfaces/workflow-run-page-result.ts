import { RunProgressModel } from './run-progress-model';

export interface WorkflowRunPageResult {
  runs: RunProgressModel[];
  totalCount: number;
}
