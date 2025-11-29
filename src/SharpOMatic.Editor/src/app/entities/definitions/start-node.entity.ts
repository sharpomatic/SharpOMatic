import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';
import { ContextEntryListEntity, ContextEntryListSnapshot } from './context-entry-list.entity';
import { computed, signal, WritableSignal } from '@angular/core';

export interface StartNodeSnapshot extends NodeSnapshot {
  applyInitialization: boolean;
  initializing: ContextEntryListSnapshot;
}

export class StartNodeEntity extends NodeEntity<StartNodeSnapshot> {
  public applyInitialization: WritableSignal<boolean>;
  public initializing: WritableSignal<ContextEntryListEntity>;

  constructor(snapshot: StartNodeSnapshot) {
    super(snapshot);

    this.applyInitialization = signal(snapshot.applyInitialization);
    this.initializing = signal(ContextEntryListEntity.fromSnapshot(snapshot.initializing));

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentIsNodeDirty = isNodeDirty();
      const currentApplyInitialization = this.applyInitialization();
      const currentInitsDirty = this.initializing().isDirty();

      return currentIsNodeDirty ||
        (currentApplyInitialization !== snapshot.applyInitialization) ||
        currentInitsDirty;
    });
  }

  public override toSnapshot(): StartNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      applyInitialization: this.applyInitialization(),
      initializing: this.initializing().toSnapshot(),
    };
  }

  public static override defaultSnapshot() {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.Start,
      title: 'Start',
      inputs: [],
      outputs: [ConnectorEntity.defaultSnapshot()],
      applyInitialization: false,
      initializing: ContextEntryListEntity.defaultSnapshot(),
    }
  }

  public static fromSnapshot(snapshot: StartNodeSnapshot): StartNodeEntity {
    return new StartNodeEntity(snapshot);
  }

  public static create(top: number, left: number): StartNodeEntity {
    return new StartNodeEntity({
      ...StartNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }

  public override markClean(): void {
    super.markClean();
    this.initializing().markClean();
  }
}
