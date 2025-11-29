import { XhrFactory } from "@angular/common";
import { signal, Signal, WritableSignal } from "@angular/core";

export interface EntitySnapshot {
  id: string;
}

export abstract class Entity<T extends EntitySnapshot> {
  public readonly id: string;

  protected snapshot: WritableSignal<T>;
  public abstract isDirty: Signal<boolean>;

  protected constructor(snapshot: T) {
    this.id = snapshot.id;
    this.snapshot = signal(snapshot);
  }

  public markClean(): void {
    this.snapshot.set({ ...this.toSnapshot()});
  }

  public abstract toSnapshot(): T;

  public static defaultSnapshot(): EntitySnapshot {
    return {
      id: crypto.randomUUID(),
    }
  }
}
