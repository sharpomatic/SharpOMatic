import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface StepStartNodeSnapshot extends NodeSnapshot {
  stepName: string;
}

export class StepStartNodeEntity extends NodeEntity<StepStartNodeSnapshot> {
  public stepName: WritableSignal<string>;

  constructor(snapshot: StepStartNodeSnapshot) {
    super(snapshot);

    this.stepName = signal(snapshot.stepName);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const original = this.snapshot();

      return isNodeDirty() || this.stepName() !== original.stepName;
    });
  }

  public override toSnapshot(): StepStartNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      stepName: this.stepName(),
    };
  }

  public static fromSnapshot(
    snapshot: StepStartNodeSnapshot,
  ): StepStartNodeEntity {
    return new StepStartNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): StepStartNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.StepStart,
      title: 'Step Start',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      stepName: '',
    };
  }

  public static create(top: number, left: number): StepStartNodeEntity {
    return new StepStartNodeEntity({
      ...StepStartNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
