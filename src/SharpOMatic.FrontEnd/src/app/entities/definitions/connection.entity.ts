import { Entity, EntitySnapshot } from './entity.entity';
import { computed, signal, Signal } from '@angular/core';

export interface ConnectionSnapshot extends EntitySnapshot {
  from: string;
  to: string;
}

export class ConnectionEntity extends Entity<ConnectionSnapshot> {
  public from: Signal<string>;
  public to: Signal<string>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: ConnectionSnapshot) {
    super(snapshot);

    this.from = signal(snapshot.from);
    this.to = signal(snapshot.to);

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentFrom = this.from();
      const currentTo = this.to();

      return (currentFrom !== snapshot.from) ||
             (currentTo !== snapshot.to);
    });
  }

  public override toSnapshot(): ConnectionSnapshot {
    return {
      id: this.id,
      version: this.version,
      from: this.from(),
      to: this.to(),
    };
  }

  public static fromSnapshot(snapshot: ConnectionSnapshot): ConnectionEntity {
    return new ConnectionEntity(snapshot);
  }

  public static create(from: string, to: string): ConnectionEntity {
    return new ConnectionEntity({
      ...Entity.defaultSnapshot(),
      from: from,
      to: to,
    });
  }
}
