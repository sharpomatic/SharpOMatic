import { Entity, EntitySnapshot } from './entity.entity';
import { computed, signal, Signal, WritableSignal } from '@angular/core';

export interface SwitchEntrySnapshot extends EntitySnapshot {
  name: string;
  code: string;
}

export class SwitchEntryEntity extends Entity<SwitchEntrySnapshot> {
  public name: WritableSignal<string>;
  public code: WritableSignal<string>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: SwitchEntrySnapshot) {
    super(snapshot);

    this.name = signal(snapshot.name);
    this.code = signal(snapshot.code);

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentName = this.name();
      const currentCode = this.code();

      return (currentName !== snapshot.name) ||
             (currentCode !== snapshot.code);
    });
  }

  public override toSnapshot(): SwitchEntrySnapshot {
    return {
      id: this.id,
      name: this.name(),
      code: this.code(),
    };
  }

  public static fromSnapshot(snapshot: SwitchEntrySnapshot): SwitchEntryEntity {
    return new SwitchEntryEntity(snapshot);
  }

  public static override defaultSnapshot(): SwitchEntrySnapshot {
    return {
      id: crypto.randomUUID(),
      name: '',
      code: '',
    };
  }
}
