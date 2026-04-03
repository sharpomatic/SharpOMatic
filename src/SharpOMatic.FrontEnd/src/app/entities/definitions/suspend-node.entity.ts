import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface SuspendNodeSnapshot extends NodeSnapshot {}

export class SuspendNodeEntity extends NodeEntity<SuspendNodeSnapshot> {
  constructor(snapshot: SuspendNodeSnapshot) {
    super(snapshot);
  }

  public override toSnapshot(): SuspendNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
    };
  }

  public static fromSnapshot(snapshot: SuspendNodeSnapshot): SuspendNodeEntity {
    return new SuspendNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): SuspendNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.Suspend,
      title: 'Suspend',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
    };
  }

  public static create(top: number, left: number): SuspendNodeEntity {
    return new SuspendNodeEntity({
      ...SuspendNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
