import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';
import { ContextEntryListEntity, ContextEntryListSnapshot } from './context-entry-list.entity';

export interface GosubNodeSnapshot extends NodeSnapshot {
  workflowId: string | null;
  applyInputMappings: boolean;
  inputMappings: ContextEntryListSnapshot;
  applyOutputMappings: boolean;
  outputMappings: ContextEntryListSnapshot;
}

export class GosubNodeEntity extends NodeEntity<GosubNodeSnapshot> {
  public workflowId: WritableSignal<string | null>;
  public applyInputMappings: WritableSignal<boolean>;
  public inputMappings: WritableSignal<ContextEntryListEntity>;
  public applyOutputMappings: WritableSignal<boolean>;
  public outputMappings: WritableSignal<ContextEntryListEntity>;

  constructor(snapshot: GosubNodeSnapshot) {
    super(snapshot);

    this.workflowId = signal(snapshot.workflowId ?? null);
    this.applyInputMappings = signal(snapshot.applyInputMappings);
    this.inputMappings = signal(ContextEntryListEntity.fromSnapshot(snapshot.inputMappings));
    this.applyOutputMappings = signal(snapshot.applyOutputMappings);
    this.outputMappings = signal(ContextEntryListEntity.fromSnapshot(snapshot.outputMappings));

    const baseIsDirty = this.isDirty;
    this.isDirty = computed(() => {
      const snapshot = this.snapshot();
      const currentIsDirty = baseIsDirty();
      const currentWorkflowId = this.workflowId();
      const currentApplyInputMappings = this.applyInputMappings();
      const currentInputMappingsDirty = this.inputMappings().isDirty();
      const currentApplyOutputMappings = this.applyOutputMappings();
      const currentOutputMappingsDirty = this.outputMappings().isDirty();

      return currentIsDirty ||
        currentWorkflowId !== snapshot.workflowId ||
        currentApplyInputMappings !== snapshot.applyInputMappings ||
        currentInputMappingsDirty ||
        currentApplyOutputMappings !== snapshot.applyOutputMappings ||
        currentOutputMappingsDirty;
    });
  }

  public override toSnapshot(): GosubNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      workflowId: this.workflowId(),
      applyInputMappings: this.applyInputMappings(),
      inputMappings: this.inputMappings().toSnapshot(),
      applyOutputMappings: this.applyOutputMappings(),
      outputMappings: this.outputMappings().toSnapshot(),
    };
  }

  public static fromSnapshot(snapshot: GosubNodeSnapshot): GosubNodeEntity {
    return new GosubNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): GosubNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.Gosub,
      title: 'Gosub',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      workflowId: null,
      applyInputMappings: false,
      inputMappings: ContextEntryListEntity.defaultSnapshot(),
      applyOutputMappings: false,
      outputMappings: ContextEntryListEntity.defaultSnapshot(),
    };
  }

  public static create(top: number, left: number): GosubNodeEntity {
    return new GosubNodeEntity({
      ...GosubNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }

  public override markClean(): void {
    super.markClean();
    this.inputMappings().markClean();
    this.outputMappings().markClean();
  }
}
