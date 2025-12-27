import { RunStatus } from "../../../enumerations/run-status";

export interface RunProgressModel {
  workflowId: string;
  runId: string;
  created: string;
  started?: string | null;
  stopped?: string | null;
  inputEntries?: string;
  inputContext?: string;
  outputContext?: string;
  customData?: string;
  runStatus: RunStatus;
  message: string;
  error: string;
}
