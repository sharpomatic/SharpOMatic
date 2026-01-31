import { Signal, WritableSignal, computed, signal } from '@angular/core';
import { EvalColumn, EvalColumnSnapshot } from './eval-column';
import { EvalDataSnapshot } from './eval-data';
import { EvalDataStore } from './eval-data-store';
import { EvalGrader, EvalGraderSnapshot } from './eval-grader';
import { EvalRow, EvalRowSnapshot } from './eval-row';

export interface EvalConfigSnapshot {
  evalConfigId: string;
  workflowId: string | null;
  name: string;
  description: string;
  maxParallel: number;
  graders?: EvalGraderSnapshot[];
  columns?: EvalColumnSnapshot[];
  rows?: EvalRowSnapshot[];
}

export class EvalConfig {
  private static readonly DEFAULT_MAX_PARALLEL = 1;

  public readonly evalConfigId: string;
  public workflowId: WritableSignal<string | null>;
  public name: WritableSignal<string>;
  public description: WritableSignal<string>;
  public maxParallel: WritableSignal<number>;
  public graders: WritableSignal<EvalGrader[]>;
  public columns: WritableSignal<EvalColumn[]>;
  public rows: WritableSignal<EvalRow[]>;
  public dataStore: EvalDataStore;
  public readonly isDirty: Signal<boolean>;

  private initialWorkflowId: string | null;
  private initialName: string;
  private initialDescription: string;
  private initialMaxParallel: number;
  private initialGraders: EvalGraderSnapshot[];
  private initialColumns: EvalColumnSnapshot[];
  private initialRows: EvalRowSnapshot[];
  private readonly cleanVersion = signal(0);

  constructor(snapshot: EvalConfigSnapshot, dataSnapshots: EvalDataSnapshot[] = []) {
    this.evalConfigId = snapshot.evalConfigId;
    this.initialWorkflowId = snapshot.workflowId ?? null;
    this.initialName = snapshot.name;
    this.initialDescription = snapshot.description;
    this.initialMaxParallel = snapshot.maxParallel;

    this.workflowId = signal(snapshot.workflowId ?? null);
    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.maxParallel = signal(snapshot.maxParallel);
    this.graders = signal(EvalConfig.gradersFromSnapshots(snapshot.graders ?? []));
    this.columns = signal(EvalConfig.columnsFromSnapshots(snapshot.columns ?? []));
    this.rows = signal(EvalConfig.rowsFromSnapshots(snapshot.rows ?? []));
    this.dataStore = new EvalDataStore(dataSnapshots);
    this.initialGraders = EvalConfig.snapshotsFromGraders(this.graders(), this.evalConfigId);
    this.initialColumns = EvalConfig.snapshotsFromColumns(this.columns(), this.evalConfigId);
    this.initialRows = EvalConfig.snapshotsFromRows(this.rows(), this.evalConfigId);

    this.isDirty = computed(() => {
      this.cleanVersion();

      const currentWorkflowId = this.workflowId();
      const currentName = this.name();
      const currentDescription = this.description();
      const currentMaxParallel = this.maxParallel();
      const currentGraders = this.graders();
      const currentColumns = this.columns();
      const currentRows = this.rows();
      const currentGraderSnapshots = EvalConfig.snapshotsFromGraders(currentGraders, this.evalConfigId);
      const currentColumnSnapshots = EvalConfig.snapshotsFromColumns(currentColumns, this.evalConfigId);
      const currentRowSnapshots = EvalConfig.snapshotsFromRows(currentRows, this.evalConfigId);

      const gradersChanged = !EvalConfig.areGradersEqual(currentGraderSnapshots, this.initialGraders);
      const columnsChanged = !EvalConfig.areColumnsEqual(currentColumnSnapshots, this.initialColumns);
      const rowsChanged = !EvalConfig.areRowsEqual(currentRowSnapshots, this.initialRows);

      return currentWorkflowId !== this.initialWorkflowId ||
             currentName !== this.initialName ||
             currentDescription !== this.initialDescription ||
             currentMaxParallel !== this.initialMaxParallel ||
             gradersChanged ||
             columnsChanged ||
             rowsChanged ||
             this.dataStore.isDirty();
    });
  }

  public toSnapshot(): EvalConfigSnapshot {
    return {
      evalConfigId: this.evalConfigId,
      workflowId: this.workflowId() ?? null,
      name: this.name(),
      description: this.description(),
      maxParallel: this.maxParallel(),
      graders: EvalConfig.snapshotsFromGraders(this.graders(), this.evalConfigId),
      columns: EvalConfig.snapshotsFromColumns(this.columns(), this.evalConfigId),
      rows: EvalConfig.snapshotsFromRows(this.rows(), this.evalConfigId),
    };
  }

  public markClean(): void {
    this.initialWorkflowId = this.workflowId() ?? null;
    this.initialName = this.name();
    this.initialDescription = this.description();
    this.initialMaxParallel = this.maxParallel();
    this.initialGraders = EvalConfig.snapshotsFromGraders(this.graders(), this.evalConfigId);
    this.initialColumns = EvalConfig.snapshotsFromColumns(this.columns(), this.evalConfigId);
    this.initialRows = EvalConfig.snapshotsFromRows(this.rows(), this.evalConfigId);
    this.dataStore.markClean();
    this.cleanVersion.update(v => v + 1);
  }

  public getDeletedGraderIds(): string[] {
    const currentIds = new Set(this.graders().map(grader => grader.evalGraderId));
    return this.initialGraders
      .filter(grader => !currentIds.has(grader.evalGraderId))
      .map(grader => grader.evalGraderId);
  }

  public static fromSnapshot(snapshot: EvalConfigSnapshot): EvalConfig {
    return new EvalConfig(snapshot);
  }

  public static defaultSnapshot(): EvalConfigSnapshot {
    return {
      evalConfigId: crypto.randomUUID(),
      workflowId: null,
      name: '',
      description: '',
      maxParallel: EvalConfig.DEFAULT_MAX_PARALLEL,
      graders: [],
      columns: [],
      rows: [],
    };
  }

  private static gradersFromSnapshots(snapshots: EvalGraderSnapshot[]): EvalGrader[] {
    return (snapshots ?? [])
      .slice()
      .sort((a, b) => a.order - b.order)
      .map(EvalGrader.fromSnapshot);
  }

  private static snapshotsFromGraders(
    graders: EvalGrader[] | EvalGraderSnapshot[],
    evalConfigId: string
  ): EvalGraderSnapshot[] {
    if (!graders) {
      return [];
    }

    if (graders.length === 0) {
      return [];
    }

    if (graders[0] instanceof EvalGrader) {
      return (graders as EvalGrader[]).map((grader, index) => grader.toSnapshot(index, evalConfigId));
    }

    return (graders as EvalGraderSnapshot[]).map((grader, index) => ({
      ...grader,
      evalConfigId,
      order: index,
    }));
  }

  private static columnsFromSnapshots(snapshots: EvalColumnSnapshot[]): EvalColumn[] {
    return (snapshots ?? [])
      .slice()
      .sort((a, b) => a.order - b.order)
      .map(EvalColumn.fromSnapshot);
  }

  private static snapshotsFromColumns(
    columns: EvalColumn[] | EvalColumnSnapshot[],
    evalConfigId: string
  ): EvalColumnSnapshot[] {
    if (!columns) {
      return [];
    }

    if (columns.length === 0) {
      return [];
    }

    if (columns[0] instanceof EvalColumn) {
      return (columns as EvalColumn[]).map((column, index) => column.toSnapshot(index, evalConfigId));
    }

    return (columns as EvalColumnSnapshot[]).map((column, index) => ({
      ...column,
      evalConfigId,
      order: index,
    }));
  }

  private static rowsFromSnapshots(snapshots: EvalRowSnapshot[]): EvalRow[] {
    return (snapshots ?? [])
      .slice()
      .sort((a, b) => a.order - b.order)
      .map(EvalRow.fromSnapshot);
  }

  private static snapshotsFromRows(
    rows: EvalRow[] | EvalRowSnapshot[],
    evalConfigId: string
  ): EvalRowSnapshot[] {
    if (!rows) {
      return [];
    }

    if (rows.length === 0) {
      return [];
    }

    if (rows[0] instanceof EvalRow) {
      return (rows as EvalRow[]).map((row, index) => row.toSnapshot(index, evalConfigId));
    }

    return (rows as EvalRowSnapshot[]).map((row, index) => ({
      ...row,
      evalConfigId,
      order: index,
    }));
  }

  private static areGradersEqual(current: EvalGraderSnapshot[], initial: EvalGraderSnapshot[]): boolean {
    if (current.length !== initial.length) {
      return false;
    }

    for (let index = 0; index < current.length; index += 1) {
      const left = current[index];
      const right = initial[index];
      if (!right) {
        return false;
      }

      if (left.evalGraderId !== right.evalGraderId ||
          left.evalConfigId !== right.evalConfigId ||
          left.workflowId !== right.workflowId ||
          left.label !== right.label ||
          left.passThreshold !== right.passThreshold ||
          left.order !== right.order) {
        return false;
      }
    }

    return true;
  }

  private static areColumnsEqual(current: EvalColumnSnapshot[], initial: EvalColumnSnapshot[]): boolean {
    if (current.length !== initial.length) {
      return false;
    }

    for (let index = 0; index < current.length; index += 1) {
      const left = current[index];
      const right = initial[index];
      if (!right) {
        return false;
      }

      if (left.evalColumnId !== right.evalColumnId ||
          left.evalConfigId !== right.evalConfigId ||
          left.name !== right.name ||
          left.entryType !== right.entryType ||
          left.optional !== right.optional ||
          left.inputPath !== right.inputPath ||
          left.order !== right.order) {
        return false;
      }
    }

    return true;
  }

  private static areRowsEqual(current: EvalRowSnapshot[], initial: EvalRowSnapshot[]): boolean {
    if (current.length !== initial.length) {
      return false;
    }

    for (let index = 0; index < current.length; index += 1) {
      const left = current[index];
      const right = initial[index];
      if (!right) {
        return false;
      }

      if (left.evalRowId !== right.evalRowId ||
          left.evalConfigId !== right.evalConfigId ||
          left.order !== right.order) {
        return false;
      }
    }

    return true;
  }
}
