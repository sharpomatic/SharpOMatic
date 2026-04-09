import { ConversationSummaryModel } from './conversation-summary-model';

export interface WorkflowConversationPageResult {
  conversations: ConversationSummaryModel[];
  totalCount: number;
}
