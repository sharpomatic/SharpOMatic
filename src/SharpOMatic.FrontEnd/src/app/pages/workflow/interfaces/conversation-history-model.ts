import { AssetSummary } from '../../assets/interfaces/asset-summary';
import { RunProgressModel } from './run-progress-model';
import { ConversationHistoryTurnModel } from './conversation-history-turn-model';

export interface ConversationHistoryModel {
  conversationId: string;
  workflowId: string;
  latestRun?: RunProgressModel | null;
  turns: ConversationHistoryTurnModel[];
  conversationAssets: AssetSummary[];
}
