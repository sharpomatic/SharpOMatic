import { Signal, computed, signal } from '@angular/core';
import { EvalDataSnapshot } from './eval-data';

export class EvalDataStore {
  private readonly initialByKey = new Map<string, EvalDataSnapshot>();
  private readonly editsByKey = new Map<string, EvalDataSnapshot>();
  private readonly dirtyCount = signal(0);

  public readonly isDirty: Signal<boolean>;

  constructor(snapshots: EvalDataSnapshot[] = []) {
    this.replaceAll(snapshots);
    this.isDirty = computed(() => this.dirtyCount() > 0);
  }

  public replaceAll(snapshots: EvalDataSnapshot[]): void {
    this.initialByKey.clear();
    this.editsByKey.clear();

    (snapshots ?? []).forEach((snapshot) => {
      const normalized = EvalDataStore.normalizeSnapshot(snapshot);
      const key = EvalDataStore.buildKey(
        normalized.evalRowId,
        normalized.evalColumnId,
      );
      this.initialByKey.set(key, normalized);
    });

    this.dirtyCount.set(0);
  }

  public getSnapshot(
    evalRowId: string,
    evalColumnId: string,
  ): EvalDataSnapshot | null {
    const key = EvalDataStore.buildKey(evalRowId, evalColumnId);
    const edited = this.editsByKey.get(key);
    if (edited) {
      return { ...edited };
    }

    const existing = this.initialByKey.get(key);
    return existing ? { ...existing } : null;
  }

  public upsert(snapshot: EvalDataSnapshot): void {
    const normalized = EvalDataStore.normalizeSnapshot(snapshot);
    const key = EvalDataStore.buildKey(
      normalized.evalRowId,
      normalized.evalColumnId,
    );
    const initial = this.initialByKey.get(key);

    if (initial && EvalDataStore.areSnapshotsEqual(initial, normalized)) {
      this.editsByKey.delete(key);
      this.dirtyCount.set(this.editsByKey.size);
      return;
    }

    if (!initial && EvalDataStore.isSnapshotEmpty(normalized)) {
      this.editsByKey.delete(key);
      this.dirtyCount.set(this.editsByKey.size);
      return;
    }

    this.editsByKey.set(key, normalized);
    this.dirtyCount.set(this.editsByKey.size);
  }

  public markClean(): void {
    if (this.editsByKey.size > 0) {
      for (const [key, value] of this.editsByKey.entries()) {
        this.initialByKey.set(key, value);
      }
      this.editsByKey.clear();
    }

    this.dirtyCount.set(0);
  }

  public getDirtySnapshots(): EvalDataSnapshot[] {
    return Array.from(this.editsByKey.values()).map((snapshot) => ({
      ...snapshot,
    }));
  }

  private static buildKey(evalRowId: string, evalColumnId: string): string {
    return `${evalRowId}|${evalColumnId}`;
  }

  private static normalizeSnapshot(
    snapshot: EvalDataSnapshot,
  ): EvalDataSnapshot {
    return {
      evalDataId: snapshot.evalDataId,
      evalRowId: snapshot.evalRowId,
      evalColumnId: snapshot.evalColumnId,
      stringValue: snapshot.stringValue ?? null,
      intValue: snapshot.intValue ?? null,
      doubleValue: snapshot.doubleValue ?? null,
      boolValue: snapshot.boolValue ?? null,
    };
  }

  private static areSnapshotsEqual(
    left: EvalDataSnapshot,
    right: EvalDataSnapshot,
  ): boolean {
    return (
      left.evalDataId === right.evalDataId &&
      left.evalRowId === right.evalRowId &&
      left.evalColumnId === right.evalColumnId &&
      left.stringValue === right.stringValue &&
      left.intValue === right.intValue &&
      left.doubleValue === right.doubleValue &&
      left.boolValue === right.boolValue
    );
  }

  private static isSnapshotEmpty(snapshot: EvalDataSnapshot): boolean {
    return (
      snapshot.stringValue === null &&
      snapshot.intValue === null &&
      snapshot.doubleValue === null &&
      snapshot.boolValue === null
    );
  }
}
