import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { ContextEntryListEntity, ContextEntryListSnapshot } from './context-entry-list.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface EditNodeSnapshot extends NodeSnapshot {
  edits: ContextEntryListSnapshot;
}

export class EditNodeEntity extends NodeEntity<EditNodeSnapshot> {
  public edits: WritableSignal<ContextEntryListEntity>;

  constructor(snapshot: EditNodeSnapshot) {
    super(snapshot);

    this.edits = signal(ContextEntryListEntity.fromSnapshot(snapshot.edits));

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      // Must touch all property signals
      const currentIsNodeDirty = isNodeDirty();
      const currentEditsDirty = this.edits().isDirty();

      return currentIsNodeDirty ||
        currentEditsDirty;
    });
  }

  public override toSnapshot(): EditNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      edits: this.edits().toSnapshot(),
    };
  }

  public static fromSnapshot(snapshot: EditNodeSnapshot): EditNodeEntity {
    return new EditNodeEntity(snapshot);
  }

  public static override defaultSnapshot() {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.Edit,
      title: 'Edit',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      edits: ContextEntryListEntity.defaultSnapshot(),
    }
  }

  public static create(top: number, left: number): EditNodeEntity {
    return new EditNodeEntity({
      ...EditNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }

  public override markClean(): void {
    super.markClean();
    this.edits().markClean();
  }
}
