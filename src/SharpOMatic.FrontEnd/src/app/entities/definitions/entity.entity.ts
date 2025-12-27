import { XhrFactory } from "@angular/common";
import { signal, Signal, WritableSignal } from "@angular/core";

export interface EntitySnapshot {
  id: string;
  version: number;
}

export abstract class Entity<T extends EntitySnapshot> {
  public static readonly DEFAULT_VERSION = 1;
  public readonly id: string;
  public readonly version: number;

  protected snapshot: WritableSignal<T>;
  public abstract isDirty: Signal<boolean>;

  protected constructor(snapshot: T) {
    this.id = snapshot.id;
    this.version = snapshot.version ?? Entity.DEFAULT_VERSION;
    this.snapshot = signal({ ...snapshot, version: this.version });
  }

  public markClean(): void {
    this.snapshot.set({ ...this.toSnapshot()});
  }

  public abstract toSnapshot(): T;

  public static defaultSnapshot(): EntitySnapshot {
    return {
      id: crypto.randomUUID(),
      version: Entity.DEFAULT_VERSION,
    }
  }
}
