import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';
import { ContextEntryListEntity, ContextEntryListSnapshot } from './context-entry-list.entity';
import { computed, signal, WritableSignal } from '@angular/core';

export interface EndNodeSnapshot extends NodeSnapshot {
  applyMappings: boolean;
  mappings: ContextEntryListSnapshot;
}

export class EndNodeEntity extends NodeEntity<EndNodeSnapshot> {
  public applyMappings: WritableSignal<boolean>;
  public mappings: WritableSignal<ContextEntryListEntity>;

  constructor(snapshot: EndNodeSnapshot) {
    super(snapshot);

    this.applyMappings = signal(snapshot.applyMappings);
    this.mappings = signal(ContextEntryListEntity.fromSnapshot(snapshot.mappings));

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentIsNodeDirty = isNodeDirty();
      const currentApplyMappings = this.applyMappings();
      const currentInitsDirty = this.mappings().isDirty();

      return currentIsNodeDirty ||
        (currentApplyMappings !== snapshot.applyMappings) ||
        currentInitsDirty;
    });
  }

  public override toSnapshot(): EndNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      applyMappings: this.applyMappings(),
      mappings: this.mappings().toSnapshot(),
    };
  }

  public static fromSnapshot(snapshot: EndNodeSnapshot): EndNodeEntity {
    return new EndNodeEntity(snapshot);
  }

  public static override defaultSnapshot() {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.End,
      title: 'End',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [],
      applyMappings: false,
      mappings: ContextEntryListEntity.defaultSnapshot(),
    }
  }

  public static create(top: number, left: number): EndNodeEntity {
    return new EndNodeEntity({
      ...EndNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }

  public override markClean(): void {
    super.markClean();
    this.mappings().markClean();
  }
}
