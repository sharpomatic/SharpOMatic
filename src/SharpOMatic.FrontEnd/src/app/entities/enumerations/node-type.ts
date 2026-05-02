export enum NodeType {
  Start = 0,
  End = 1,
  Code = 2,
  Edit = 3,
  Switch = 4,
  FanIn = 5,
  FanOut = 6,
  ModelCall = 7,
  Batch = 8,
  Gosub = 9,
  Suspend = 10,
  FrontendToolCall = 11,
  BackendToolCall = 12,
  StepStart = 13,
  StepEnd = 14,
  ActivitySync = 15,
  StateSync = 16,
  EventTemplate = 17,
}

export function getNodeSymbol(nodeType: NodeType): string {
  switch (nodeType) {
    case NodeType.Start:
      return 'bi-chevron-bar-left';
    case NodeType.End:
      return 'bi-chevron-bar-right';
    case NodeType.FanIn:
      return 'bi-box-arrow-in-right';
    case NodeType.FanOut:
      return 'bi-box-arrow-right';
    case NodeType.Switch:
      return 'bi-option';
    case NodeType.Edit:
      return 'bi-pencil';
    case NodeType.Code:
      return 'bi-filetype-cs';
    case NodeType.ModelCall:
      return 'bi-chat-text';
    case NodeType.Batch:
      return 'bi-layers';
    case NodeType.Gosub:
      return 'bi-diagram-3';
    case NodeType.Suspend:
      return 'bi-pause-circle';
    case NodeType.FrontendToolCall:
      return 'bi-hand-index';
    case NodeType.BackendToolCall:
      return 'bi-server';
    case NodeType.StepStart:
      return 'bi-play-circle';
    case NodeType.StepEnd:
      return 'bi-stop-circle';
    case NodeType.ActivitySync:
      return 'bi-arrow-repeat';
    case NodeType.StateSync:
      return 'bi-arrow-repeat';
    case NodeType.EventTemplate:
      return 'bi-card-text';
    default:
      return '';
  }
}
