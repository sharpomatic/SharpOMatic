import { EvalRunExportField } from '../enumerations/eval-run-export-field';

export interface EvalRunCsvExportRequest {
  columnIds: string[];
  fields: EvalRunExportField[];
  graderPaths: string[];
}
