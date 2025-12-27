export enum NodeType {
  Start = 0,
  End = 1,
  Code = 2,
  Edit = 3,
  Switch = 4,
  FanIn = 5,
  FanOut = 6,
  ModelCall = 7,
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
    default:
      return '';
  }
}
