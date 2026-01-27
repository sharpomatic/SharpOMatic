import { Injectable, OnDestroy, WritableSignal, effect, inject, signal, untracked } from '@angular/core';
import { ServerRepositoryService } from '../../../services/server.repository.service';
import { WorkflowEntity } from '../../../entities/definitions/workflow.entity';
import { RunProgressModel } from '../interfaces/run-progress-model';
import { TraceProgressModel } from '../interfaces/trace-progress-model';
import { SignalrService } from '../../../services/signalr.service';
import { RunStatus } from '../../../enumerations/run-status';
import { NodeStatus } from '../../../enumerations/node-status';
import { Observable, map } from 'rxjs';
import { ContextEntryListEntity, ContextEntryListSnapshot } from '../../../entities/definitions/context-entry-list.entity';
import { ContextEntryEntity, ContextEntrySnapshot } from '../../../entities/definitions/context-entry.entity';
import { StartNodeEntity } from '../../../entities/definitions/start-node.entity';
import { ToastService } from '../../../services/toast.service';
import { RunSortField } from '../../../enumerations/run-sort-field';
import { SortDirection } from '../../../enumerations/sort-direction';
import { AssetSummary } from '../../assets/interfaces/asset-summary';
import { AssetScope } from '../../../enumerations/asset-scope';
import { AssetSortField } from '../../../enumerations/asset-sort-field';

@Injectable({
  providedIn: 'root',
})
export class WorkflowService implements OnDestroy  {
  private readonly serverWorkflowService = inject(ServerRepositoryService);
  private readonly toastService = inject(ToastService);
  public readonly signalrService = inject(SignalrService);
  public readonly runsPageSize = 50;

  public workflow: WritableSignal<WorkflowEntity>;
  public runProgress: WritableSignal<RunProgressModel | undefined>;
  public traces: WritableSignal<TraceProgressModel[]>;
  public runAssets: WritableSignal<AssetSummary[]>;
  public runs: WritableSignal<RunProgressModel[]>;
  public runsTotal: WritableSignal<number>;
  public runsPage: WritableSignal<number>;
  public runsSortField: WritableSignal<RunSortField>;
  public runsSortDirection: WritableSignal<SortDirection>;
  public isRunning: WritableSignal<boolean>;
  public runInputs: WritableSignal<ContextEntryListEntity>;
  private lastStartInputsSnapshot?: ContextEntryListSnapshot;

  // Create a stable references for listener functions, so they run in correct zone
  private readonly runProgressListener = (data: RunProgressModel) => this.onRunProgress(data);
  private readonly traceProgressListener = (data: TraceProgressModel) => this.onTraceProgress(data);

  constructor() {
    this.workflow = signal(new WorkflowEntity(WorkflowEntity.defaultSnapshot()));
    this.runProgress = signal(undefined);
    this.traces = signal([]);
    this.runAssets = signal([]);
    this.runs = signal([]);
    this.runsTotal = signal(0);
    this.runsPage = signal(1);
    this.runsSortField = signal(RunSortField.Created);
    this.runsSortDirection = signal(SortDirection.Descending);
    this.isRunning = signal(false);
    this.runInputs = signal(ContextEntryListEntity.fromSnapshot(ContextEntryListEntity.defaultSnapshot()));

    effect(() => {
      if (this.signalrService.isConnected()) {
        this.addListeners();
      } else {
        this.removeListeners();
      }
    });

    effect(() => {
      const workflow = this.workflow();
      const startNode = workflow.nodes().find((node): node is StartNodeEntity => node instanceof StartNodeEntity);
      const snapshot = startNode ? startNode.initializing().toSnapshot() : undefined;
      this.syncRunInputsWithStartSnapshot(snapshot);
    });
  }

  ngOnDestroy(): void {
    this.removeListeners();
    this.markClean();
  }

  load(id: string) {
    this.serverWorkflowService.getWorkflow(id).subscribe(workflow => {
      this.workflow.set(workflow as WorkflowEntity);
      this.workflow().nodes().forEach(nodeEntity => nodeEntity.displayState.set(NodeStatus.None));
      this.runProgress.set(undefined);
      this.runsTotal.set(0);
      this.runsPage.set(1);
      this.runsSortField.set(RunSortField.Created);
      this.runsSortDirection.set(SortDirection.Descending);
      this.runAssets.set([]);
      this.updateRunInputsFromWorkflow();
      this.workflow().markClean();
      this.loadRunsPageForWorkflow(id, 1);
      this.serverWorkflowService.getLatestWorkflowRun(id).subscribe(run => {
        if (run) {
          this.runProgress.set(run);
          this.updateRunInputsFromLatestRun();          
          this.serverWorkflowService.getRunTraces(run.runId).subscribe(traces => {
            if (traces) {
              this.traces.set(traces);
              const nodes = this.workflow().nodes();
              traces.forEach(t => {
                const nodeEntity = nodes.find(n => n.id == t.nodeEntityId);
                if (nodeEntity) {
                  nodeEntity.displayState.set(t.nodeStatus);
                }
              })
            }
          });
          this.updateRunAssetsForRun(run);
          return;
        }
        this.runAssets.set([]);
      });
    });
  };

  save(): Observable<void> {
    return this.serverWorkflowService.upsertWorkflow(this.workflow()).pipe(
      map(() => {
        this.workflow().markClean();
        return;
      })
    );
  }

  run(): Observable<string | undefined> {
    this.isRunning.set(true);
    return this.serverWorkflowService.runWorkflow(this.workflow().id, this.runInputs());
  }

  markClean(): void {
    this.workflow().markClean();
    this.runInputs().markClean();
  }

  addListeners(): void {
    this.signalrService.addListener("RunProgress", this.runProgressListener);
    this.signalrService.addListener("TraceProgress", this.traceProgressListener);
  }

  removeListeners(): void {
    this.signalrService.removeListener("RunProgress", this.runProgressListener);
    this.signalrService.removeListener("TraceProgress", this.traceProgressListener);
  }

  onRunProgress(data: RunProgressModel) {
    const workflow = this.workflow();
    if (workflow) {
      switch(data.runStatus) {
        case RunStatus.Created: {
            workflow.nodes().forEach(nodeEntity => nodeEntity.displayState.set(NodeStatus.None));
            this.runProgress.set(data);
            this.traces.set([]);
            this.runAssets.set([]);
            break;
        }
        case RunStatus.Running: {
          this.runProgress.set(data);
          break;
        }
        case RunStatus.Success: {
          this.runProgress.set(data);
          this.isRunning.set(false);
          const workflowName = workflow.name();
          const successMessage = `${workflowName} completed successfully.`;
          this.toastService.success(successMessage);
          this.updateRunAssetsForRun(data);
          break;
        }
        case RunStatus.Failed: {
          this.runProgress.set(data);
          this.isRunning.set(false);
          const workflowName = workflow.name();
          const errorMessage = (data.error ?? '').trim();
          const failureMessage = errorMessage ? `${workflowName} failed: ${errorMessage}` : `${workflowName} failed.`;
          this.toastService.error(failureMessage);
          this.updateRunAssetsForRun(data);
          break;
        }
      }
    }
  }

  onTraceProgress(data: TraceProgressModel) {
    const workflow = this.workflow();
    if (workflow) {
      const nodeEntity = workflow.nodes().find(n => n.id == data.nodeEntityId);
      if (nodeEntity) {
            nodeEntity.displayState.set(data.nodeStatus);
      }
      if (data.nodeStatus === NodeStatus.Running) {
        this.traces.update(traces => [...traces, data]);
      } else {
        const traces = this.traces();
        const idx = traces.findIndex(t => t.traceId === data.traceId);
        if (idx >= 0) {
          traces[idx] = data;
          this.traces.set([...traces]);
        }
      }
    }
  }

  private updateRunInputsFromWorkflow(): void {
    const workflow = this.workflow();
    const startNode = workflow.nodes().find((node): node is StartNodeEntity => node instanceof StartNodeEntity);

    if (startNode) {
      const snapshot = startNode.initializing().toSnapshot();
      this.runInputs.set(ContextEntryListEntity.fromSnapshot(snapshot));
    } else {
      this.runInputs.set(ContextEntryListEntity.fromSnapshot(ContextEntryListEntity.defaultSnapshot()));
    }
  }

  private updateRunInputsFromLatestRun(): void {
    const run = this.runProgress();
    const inputEntriesJson = run?.inputEntries;
    if (!inputEntriesJson) {
      return;
    }

    let snapshot: ContextEntryListSnapshot | undefined;

    try {
      const parsed = JSON.parse(inputEntriesJson);
      if (parsed && typeof parsed === 'object' && Array.isArray(parsed.entries)) {
        snapshot = parsed as ContextEntryListSnapshot;
      }
    } catch {
      // Invalid payload; nothing to update.
      return;
    }

    if (!snapshot) {
      return;
    }

    const previousInputs = ContextEntryListEntity.fromSnapshot(snapshot);
    const previousEntriesByPath = new Map<string, ContextEntryEntity>();
    previousInputs.entries().forEach(entry => {
      previousEntriesByPath.set(entry.inputPath(), entry);
    });

    this.runInputs().entries().forEach(entry => {
      const previousEntry = previousEntriesByPath.get(entry.inputPath());
      if (!previousEntry) {
        return;
      }

      if (!entry.optional()) {
        entry.entryType.set(previousEntry.entryType());
        entry.entryValue.set(previousEntry.entryValue());
        return;
      }

      if (entry.entryType() === previousEntry.entryType()) {
        entry.entryValue.set(previousEntry.entryValue());
      }
    });
  }

  public loadRunsPage(page: number): void {
    const workflowId = this.workflow().id;
    if (!workflowId) {
      return;
    }

    const normalizedPage = Number.isFinite(page) ? Math.max(1, Math.floor(page)) : 1;
    this.loadRunsPageForWorkflow(workflowId, normalizedPage);
  }

  public updateRunsSort(field: RunSortField): void {
    const workflowId = this.workflow().id;
    if (this.runsSortField() === field) {
      const nextDirection = this.runsSortDirection() === SortDirection.Descending
        ? SortDirection.Ascending
        : SortDirection.Descending;
      this.runsSortDirection.set(nextDirection);
    } else {
      this.runsSortField.set(field);
      this.runsSortDirection.set(SortDirection.Descending);
    }

    if (!workflowId) {
      return;
    }

    this.loadRunsPageForWorkflow(workflowId, 1);
  }

  private loadRunsPageForWorkflow(workflowId: string, page: number): void {
    const sortBy = this.runsSortField();
    const sortDirection = this.runsSortDirection();
    this.serverWorkflowService.getLatestWorkflowRuns(workflowId, page, this.runsPageSize, sortBy, sortDirection).subscribe(result => {
      if (!result) {
        this.runs.set([]);
        this.runsTotal.set(0);
        return;
      }

      const totalCount = result.totalCount ?? 0;
      const totalPages = this.getRunsPageCount(totalCount);
      if (totalPages > 0 && page > totalPages) {
        this.loadRunsPageForWorkflow(workflowId, totalPages);
        return;
      }

      this.runs.set(result.runs ?? []);
      this.runsTotal.set(totalCount);
      this.runsPage.set(page);
    });
  }

  public getRunsPageCount(totalCount = this.runsTotal()): number {
    if (totalCount <= 0) {
      return 0;
    }

    return Math.ceil(totalCount / this.runsPageSize);
  }

  private syncRunInputsWithStartSnapshot(snapshot?: ContextEntryListSnapshot): void {
    if (!snapshot) {
      this.lastStartInputsSnapshot = undefined;
      return;
    }

    const previousSnapshot = this.lastStartInputsSnapshot;

    untracked(() => {
      const runInputs = this.runInputs();
      const currentEntries = runInputs.entries();
      const currentEntriesByPath = new Map<string, ContextEntryEntity>();
      currentEntries.forEach(entry => currentEntriesByPath.set(entry.inputPath(), entry));
      const previousEntriesByPath = new Map<string, ContextEntrySnapshot>();
      previousSnapshot?.entries.forEach(entry => previousEntriesByPath.set(entry.inputPath, entry));

      let needsUpdate = currentEntries.length !== snapshot.entries.length;
      const nextEntries: ContextEntryEntity[] = [];

      snapshot.entries.forEach((entrySnapshot, index) => {
        const existingEntry = currentEntriesByPath.get(entrySnapshot.inputPath);
        const previousEntrySnapshot = previousEntriesByPath.get(entrySnapshot.inputPath);
        let entry: ContextEntryEntity;

        if (existingEntry) {
          entry = existingEntry;
          currentEntriesByPath.delete(entrySnapshot.inputPath);
        } else {
          entry = ContextEntryEntity.fromSnapshot(entrySnapshot);
          needsUpdate = true;
        }

        const entryUpdated = this.applyEntrySnapshot(entry, entrySnapshot, previousEntrySnapshot);
        needsUpdate = needsUpdate || entryUpdated;

        if (!needsUpdate && currentEntries[index] !== entry) {
          needsUpdate = true;
        }

        nextEntries.push(entry);
      });

      if (currentEntriesByPath.size > 0) {
        needsUpdate = true;
      }

      if (needsUpdate) {
        runInputs.entries.set(nextEntries);
      }
    });

    this.lastStartInputsSnapshot = snapshot;
  }

  private applyEntrySnapshot(entry: ContextEntryEntity, snapshot: ContextEntrySnapshot, previousSnapshot?: ContextEntrySnapshot): boolean {
    let changed = false;

    if (entry.purpose() !== snapshot.purpose) {
      entry.purpose.set(snapshot.purpose);
      changed = true;
    }

    if (entry.inputPath() !== snapshot.inputPath) {
      entry.inputPath.set(snapshot.inputPath);
      changed = true;
    }

    if (entry.outputPath() !== snapshot.outputPath) {
      entry.outputPath.set(snapshot.outputPath);
      changed = true;
    }

    if (entry.optional() !== snapshot.optional) {
      entry.optional.set(snapshot.optional);
      changed = true;
    }

    const currentType = entry.entryType();
    const typeChanged = currentType !== snapshot.entryType;
    if (typeChanged) {
      entry.entryType.set(snapshot.entryType);
      changed = true;
    }

    const shouldUpdateValue =
      typeChanged ||
      !previousSnapshot ||
      previousSnapshot.entryValue !== snapshot.entryValue;

    if (shouldUpdateValue && entry.entryValue() !== snapshot.entryValue) {
      entry.entryValue.set(snapshot.entryValue);
      changed = true;
    }

    return changed;
  }

  private updateRunAssetsForRun(run?: RunProgressModel | null): void {
    if (!run?.runId) {
      this.runAssets.set([]);
      return;
    }

    if (run.runStatus !== RunStatus.Success && run.runStatus !== RunStatus.Failed) {
      this.runAssets.set([]);
      return;
    }

    this.serverWorkflowService.getAssets(
      AssetScope.Run,
      0,
      0,
      AssetSortField.Created,
      SortDirection.Descending,
      '',
      run.runId
    ).subscribe(assets => {
      this.runAssets.set(assets ?? []);
    });
  }
}
