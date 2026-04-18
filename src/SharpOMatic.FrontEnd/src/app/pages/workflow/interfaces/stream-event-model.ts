import { StreamEventKind } from '../../../enumerations/stream-event-kind';
import { StreamMessageRole } from '../../../enumerations/stream-message-role';

export interface StreamEventModel {
  streamEventId: string;
  runId: string;
  workflowId: string;
  conversationId?: string | null;
  sequenceNumber: number;
  created: string;
  eventKind: StreamEventKind;
  messageId?: string | null;
  messageRole?: StreamMessageRole | null;
  activityType?: string | null;
  replace?: boolean | null;
  textDelta?: string | null;
  toolCallId?: string | null;
  parentMessageId?: string | null;
  metadata?: string | null;
}
