import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface InputNodeSnapshot extends NodeSnapshot {}

export class InputNodeEntity extends NodeEntity<InputNodeSnapshot> {
  constructor(snapshot: InputNodeSnapshot) {
    super(snapshot);
  }

  public override toSnapshot(): InputNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
    };
  }

  public static fromSnapshot(snapshot: InputNodeSnapshot): InputNodeEntity {
    return new InputNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): InputNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.Input,
      title: 'Input',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
    };
  }

  public static create(top: number, left: number): InputNodeEntity {
    return new InputNodeEntity({
      ...InputNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
