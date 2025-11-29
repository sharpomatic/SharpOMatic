import { CodeNodeEntity, CodeNodeSnapshot } from "./code-node.entity";
import { EditNodeEntity, EditNodeSnapshot } from "./edit-node.entity";
import { EndNodeEntity, EndNodeSnapshot } from "./end-node.entity";
import { NodeEntity, NodeSnapshot } from "./node.entity";
import { StartNodeEntity, StartNodeSnapshot } from "./start-node.entity";
import { NodeType } from '../enumerations/node-type';
import { SwitchNodeEntity, SwitchNodeSnapshot } from "./switch-node.entity";
import { FanInNodeEntity, FanInNodeSnapshot } from "./fan-in-node.entity";
import { FanOutNodeEntity, FanOutNodeSnapshot } from "./fan-out-node.entity";

export function nodeFromSnapshot(snapshot: NodeSnapshot): NodeEntity<NodeSnapshot> {
  switch (snapshot.nodeType) {
    case NodeType.Start:
      return StartNodeEntity.fromSnapshot(snapshot as StartNodeSnapshot);
    case NodeType.End:
      return EndNodeEntity.fromSnapshot(snapshot as EndNodeSnapshot);
    case NodeType.Code:
      return CodeNodeEntity.fromSnapshot(snapshot as CodeNodeSnapshot);
    case NodeType.Edit:
      return EditNodeEntity.fromSnapshot(snapshot as EditNodeSnapshot);
    case NodeType.Switch:
      return SwitchNodeEntity.fromSnapshot(snapshot as SwitchNodeSnapshot);
    case NodeType.FanIn:
      return FanInNodeEntity.fromSnapshot(snapshot as FanInNodeSnapshot);
    case NodeType.FanOut:
      return FanOutNodeEntity.fromSnapshot(snapshot as FanOutNodeSnapshot);
  }
}
