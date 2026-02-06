import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { computed, signal, WritableSignal } from '@angular/core';
import { NodeType } from '../enumerations/node-type';

export interface CodeNodeSnapshot extends NodeSnapshot {
  code: string;
}

export class CodeNodeEntity extends NodeEntity<CodeNodeSnapshot> {
  public code: WritableSignal<string>;

  constructor(snapshot: CodeNodeSnapshot) {
    super(snapshot);

    this.code = signal(snapshot.code);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentIsNodeDirty = isNodeDirty();
      const currentCode = this.code();

      return currentIsNodeDirty || currentCode !== snapshot.code;
    });
  }

  public override toSnapshot(): CodeNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      code: this.code(),
    };
  }

  public static fromSnapshot(snapshot: CodeNodeSnapshot): CodeNodeEntity {
    return new CodeNodeEntity(snapshot);
  }

  public static override defaultSnapshot() {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.Code,
      title: 'Code',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      code: '',
    };
  }

  public static create(top: number, left: number): CodeNodeEntity {
    return new CodeNodeEntity({
      ...CodeNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
