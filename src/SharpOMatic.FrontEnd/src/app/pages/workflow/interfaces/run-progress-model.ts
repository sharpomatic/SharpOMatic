import { RunStatus } from '../../../enumerations/run-status';

export interface RunProgressModel {
  workflowId: string;
  runId: string;
  conversationId?: string | null;
  turnNumber?: number | null;
  created: string;
  needsEditorEvents: boolean;
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
