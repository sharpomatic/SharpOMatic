import { ContextEntryEntity, ContextEntrySnapshot } from './context-entry.entity';
import { ContextEntryPurpose } from '../enumerations/context-entry-purpose';
import { Entity, EntitySnapshot } from './entity.entity';
import { computed, signal, Signal, WritableSignal } from '@angular/core';

export interface ContextEntryListSnapshot extends EntitySnapshot {
  entries: ContextEntrySnapshot[];
}

export class ContextEntryListEntity extends Entity<ContextEntryListSnapshot> {
  public entries: WritableSignal<ContextEntryEntity[]>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: ContextEntryListSnapshot) {
    super(snapshot);

    this.entries = signal(snapshot.entries.map(ContextEntryEntity.fromSnapshot));

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();
      const snaphotEntries = snapshot.entries;

      // Must touch all property signals
      const currentEntries = this.entries();

      // Must touch all entry dirty signals
      const currentEntriesDirty = currentEntries.reduce((dirty, entry) => entry.isDirty() || dirty, false);

      return (currentEntries.length !== snaphotEntries.length) ||
             currentEntriesDirty;
    });
  }

  public appendEntry(overrides: any): void {
    const snapshot = {
      ...ContextEntryEntity.defaultSnapshot(),
      ...overrides
    };

    const newEntry = ContextEntryEntity.fromSnapshot(snapshot);
    this.entries.update(currentEntries => [...currentEntries, newEntry]);
  }

  public insertEntry(index: number, overrides: any): void {
    const snapshot = {
      ...ContextEntryEntity.defaultSnapshot(),
      ...overrides
    };

    const newEntry = ContextEntryEntity.fromSnapshot(snapshot);
    this.entries.update(currentEntries => {
      const clampedIndex = Math.max(0, Math.min(index, currentEntries.length));
      return [
        ...currentEntries.slice(0, clampedIndex),
        newEntry,
        ...currentEntries.slice(clampedIndex)
      ];
    });
  }

  public moveEntry(entryId: string, direction: 'up' | 'down', purpose: ContextEntryPurpose): void {
    const delta = direction === 'up' ? -1 : 1;
    this.entries.update(currentEntries => {
      const index = currentEntries.findIndex(entry => entry.id === entryId);
      if (index === -1) {
        return currentEntries;
      }

      let targetIndex = index + delta;
      while (targetIndex >= 0 && targetIndex < currentEntries.length) {
        if (currentEntries[targetIndex].purpose() === purpose) {
          const updatedEntries = [...currentEntries];
          const [movingEntry] = updatedEntries.splice(index, 1);
          updatedEntries.splice(targetIndex, 0, movingEntry);
          return updatedEntries;
        }

        targetIndex += delta;
      }

      return currentEntries;
    });
  }

  public deleteEntry(entryId: string): void {
    this.entries.update(currentEntries => currentEntries.filter(entry => entry.id !== entryId));
  }

  public override toSnapshot(): ContextEntryListSnapshot {
    return {
      id: this.id,
      entries: this.entries().map(entry => entry.toSnapshot()),
    };
  }

  public static fromSnapshot(snapshot: ContextEntryListSnapshot): ContextEntryListEntity {
    return new ContextEntryListEntity(snapshot);
  }

  public static override defaultSnapshot() {
    return {
      ...Entity.defaultSnapshot(),
      entries: [],
    }
  }

  public override markClean(): void {
    super.markClean();
    this.entries().forEach(entry => entry.markClean());
  }
}
