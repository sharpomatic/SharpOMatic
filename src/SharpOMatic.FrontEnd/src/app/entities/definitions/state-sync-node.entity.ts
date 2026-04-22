import { computed, signal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface StateSyncNodeSnapshot extends NodeSnapshot {
  snapshotsOnly: boolean;
}

export class StateSyncNodeEntity extends NodeEntity<StateSyncNodeSnapshot> {
  public snapshotsOnly = signal(false);

  constructor(snapshot: StateSyncNodeSnapshot) {
    super(snapshot);
    this.snapshotsOnly.set(snapshot.snapshotsOnly);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => isNodeDirty() || this.snapshotsOnly() !== this.snapshot().snapshotsOnly);
  }

  public override toSnapshot(): StateSyncNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      snapshotsOnly: this.snapshotsOnly(),
    };
  }

  public static fromSnapshot(snapshot: StateSyncNodeSnapshot): StateSyncNodeEntity {
    return new StateSyncNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): StateSyncNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.StateSync,
      title: 'State Sync',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      snapshotsOnly: false,
    };
  }

  public static create(top: number, left: number): StateSyncNodeEntity {
    return new StateSyncNodeEntity({
      ...StateSyncNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
