import { AssetSummary } from '../../assets/interfaces/asset-summary';
import { InformationProgressModel } from './information-progress-model';
import { RunProgressModel } from './run-progress-model';
import { StreamEventModel } from './stream-event-model';
import { TraceProgressModel } from './trace-progress-model';

export interface ConversationHistoryTurnModel {
  run: RunProgressModel;
  traces: TraceProgressModel[];
  informations: InformationProgressModel[];
  streamEvents: StreamEventModel[];
  assets: AssetSummary[];
}
