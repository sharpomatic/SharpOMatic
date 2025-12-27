import { NodeStatus } from "../../../enumerations/node-status";
import { NodeType } from "../../../entities/enumerations/node-type";

export interface TraceProgressModel {
  traceId: string;
  runId: string;
  workflowId: string;
  nodeEntityId: string;
  nodeType: NodeType;
  nodeStatus: NodeStatus;
  title: string;
  inputContext?: string;
  outputContext?: string;
  message?: string;
  error?: string;
}

