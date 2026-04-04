import { ConversationStatus } from '../../../enumerations/conversation-status';

export interface ConversationSummaryModel {
  conversationId: string;
  workflowId: string;
  status: ConversationStatus;
  created: string;
  updated: string;
  currentTurnNumber: number;
  lastRunId?: string | null;
  lastError?: string | null;
}
