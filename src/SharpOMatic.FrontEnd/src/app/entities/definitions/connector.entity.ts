import { computed, signal, Signal, WritableSignal } from '@angular/core';
import { Entity, EntitySnapshot } from './entity.entity';

export interface ConnectorSnapshot extends EntitySnapshot {
  name: string;
  boxOffset: number;
  labelOffsetV: number;
  labelOffsetH: number;
}

export class ConnectorEntity extends Entity<ConnectorSnapshot> {
  public static readonly DISPLAY_SIZE = 16;

  public nodeId: string = '';
  public name: WritableSignal<string>;
  public boxOffset: WritableSignal<number>;
  public labelOffsetV: WritableSignal<number>;
  public labelOffsetH: WritableSignal<number>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: ConnectorSnapshot) {
    super(snapshot);

    this.name = signal(snapshot.name);
    this.boxOffset = signal(snapshot.boxOffset);
    this.labelOffsetV = signal(snapshot.labelOffsetV);
    this.labelOffsetH = signal(snapshot.labelOffsetH);

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentName = this.name();
      const currentBoxOffset = this.boxOffset();
      const currentLabelOffsetV = this.labelOffsetV();
      const currentLabelOffsetH = this.labelOffsetH();

      return (currentName !== snapshot.name) ||
             (currentBoxOffset !== snapshot.boxOffset) ||
             (currentLabelOffsetV !== snapshot.labelOffsetV) ||
             (currentLabelOffsetH !== snapshot.labelOffsetH);
    });
  }

  public override toSnapshot(): ConnectorSnapshot {
    return {
      id: this.id,
      version: this.version,
      name: this.name(),
      boxOffset: this.boxOffset(),
      labelOffsetV: this.labelOffsetV(),
      labelOffsetH: this.labelOffsetH(),
    };
  }

  public static fromSnapshot(snapshot: ConnectorSnapshot): ConnectorEntity {
    return new ConnectorEntity(snapshot);
  }

  public static override defaultSnapshot(): ConnectorSnapshot {
    return {
      ...Entity.defaultSnapshot(),
      name: '',
      boxOffset: 0,
      labelOffsetV: 0,
      labelOffsetH: 0,
    }
  }
}
