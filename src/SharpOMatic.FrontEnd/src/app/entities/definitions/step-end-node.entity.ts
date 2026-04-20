import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface StepEndNodeSnapshot extends NodeSnapshot {
  stepName: string;
}

export class StepEndNodeEntity extends NodeEntity<StepEndNodeSnapshot> {
  public stepName: WritableSignal<string>;

  constructor(snapshot: StepEndNodeSnapshot) {
    super(snapshot);

    this.stepName = signal(snapshot.stepName);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const original = this.snapshot();

      return isNodeDirty() || this.stepName() !== original.stepName;
    });
  }

  public override toSnapshot(): StepEndNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      stepName: this.stepName(),
    };
  }

  public static fromSnapshot(snapshot: StepEndNodeSnapshot): StepEndNodeEntity {
    return new StepEndNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): StepEndNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.StepEnd,
      title: 'Step End',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      stepName: '',
    };
  }

  public static create(top: number, left: number): StepEndNodeEntity {
    return new StepEndNodeEntity({
      ...StepEndNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
