import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface ActivitySyncNodeSnapshot extends NodeSnapshot {
  instanceName: string;
  activityType: string;
  contextPath: string;
  initialReplace: boolean;
  snapshotsOnly: boolean;
}

export class ActivitySyncNodeEntity extends NodeEntity<ActivitySyncNodeSnapshot> {
  public instanceName: WritableSignal<string>;
  public activityType: WritableSignal<string>;
  public contextPath: WritableSignal<string>;
  public initialReplace: WritableSignal<boolean>;
  public snapshotsOnly: WritableSignal<boolean>;

  constructor(snapshot: ActivitySyncNodeSnapshot) {
    super(snapshot);

    this.instanceName = signal(snapshot.instanceName);
    this.activityType = signal(snapshot.activityType);
    this.contextPath = signal(snapshot.contextPath);
    this.initialReplace = signal(snapshot.initialReplace);
    this.snapshotsOnly = signal(snapshot.snapshotsOnly);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const original = this.snapshot();

      return (
        isNodeDirty() ||
        this.instanceName() !== original.instanceName ||
        this.activityType() !== original.activityType ||
        this.contextPath() !== original.contextPath ||
        this.initialReplace() !== original.initialReplace ||
        this.snapshotsOnly() !== original.snapshotsOnly
      );
    });
  }

  public override toSnapshot(): ActivitySyncNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      instanceName: this.instanceName(),
      activityType: this.activityType(),
      contextPath: this.contextPath(),
      initialReplace: this.initialReplace(),
      snapshotsOnly: this.snapshotsOnly(),
    };
  }

  public static fromSnapshot(snapshot: ActivitySyncNodeSnapshot): ActivitySyncNodeEntity {
    return new ActivitySyncNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): ActivitySyncNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.ActivitySync,
      title: 'Activity Sync',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      instanceName: '',
      activityType: '',
      contextPath: '',
      initialReplace: false,
      snapshotsOnly: false,
    };
  }

  public static create(top: number, left: number): ActivitySyncNodeEntity {
    return new ActivitySyncNodeEntity({
      ...ActivitySyncNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
