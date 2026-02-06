import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface FanInNodeSnapshot extends NodeSnapshot {
  mergePath: string;
}

export class FanInNodeEntity extends NodeEntity<FanInNodeSnapshot> {
  public mergePath: WritableSignal<string>;

  constructor(snapshot: FanInNodeSnapshot) {
    super(snapshot);

    this.mergePath = signal(snapshot.mergePath);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const currentIsNodeDirty = isNodeDirty();
      const currentMergePath = this.mergePath();
      const currentSnapshot = this.snapshot();

      return (
        currentIsNodeDirty || currentMergePath !== currentSnapshot.mergePath
      );
    });
  }

  public override toSnapshot(): FanInNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      mergePath: this.mergePath(),
    };
  }

  public static fromSnapshot(snapshot: FanInNodeSnapshot): FanInNodeEntity {
    return new FanInNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): FanInNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.FanIn,
      title: 'Fan In',
      mergePath: 'output',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
    };
  }

  public static create(top: number, left: number): FanInNodeEntity {
    return new FanInNodeEntity({
      ...FanInNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
