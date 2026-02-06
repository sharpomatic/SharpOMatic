import { computed, Signal, signal, WritableSignal } from '@angular/core';
import { Entity, EntitySnapshot } from './entity.entity';

export interface WorkflowSummarySnapshot extends EntitySnapshot {
  name: string;
  description: string;
}

export class WorkflowSummaryEntity extends Entity<WorkflowSummarySnapshot> {
  public name: WritableSignal<string>;
  public description: WritableSignal<string>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: WorkflowSummarySnapshot) {
    super(snapshot);

    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentName = this.name();
      const currentDescription = this.description();

      return (
        currentName !== snapshot.name ||
        currentDescription !== snapshot.description
      );
    });
  }

  public override toSnapshot(): WorkflowSummarySnapshot {
    return {
      id: this.id,
      version: this.version,
      name: this.name(),
      description: this.description(),
    };
  }

  public static override defaultSnapshot() {
    return {
      ...Entity.defaultSnapshot(),
      name: 'Untitled',
      description: '',
    };
  }

  public static fromSnapshot(
    snapshot: WorkflowSummarySnapshot,
  ): WorkflowSummaryEntity {
    return new WorkflowSummaryEntity(snapshot);
  }
}
