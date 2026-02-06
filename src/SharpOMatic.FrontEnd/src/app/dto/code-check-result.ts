import * as monaco from 'monaco-editor';

export interface CodeCheckResult {
  severity: monaco.MarkerSeverity;
  from: number;
  to: number;
  id: string;
  message: string;
}
