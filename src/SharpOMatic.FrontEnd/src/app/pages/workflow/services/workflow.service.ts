import {
  Injectable,
  OnDestroy,
  WritableSignal,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { ServerRepositoryService } from '../../../services/server.repository.service';
import { WorkflowEntity } from '../../../entities/definitions/workflow.entity';
import { RunProgressModel } from '../interfaces/run-progress-model';
import { TraceProgressModel } from '../interfaces/trace-progress-model';
import { InformationProgressModel } from '../interfaces/information-progress-model';
import { SignalrService } from '../../../services/signalr.service';
import { RunStatus } from '../../../enumerations/run-status';
import { NodeStatus } from '../../../enumerations/node-status';
import { Observable, map, of } from 'rxjs';
import {
  ContextEntryListEntity,
  ContextEntryListSnapshot,
} from '../../../entities/definitions/context-entry-list.entity';
import {
  ContextEntryEntity,
  ContextEntrySnapshot,
} from '../../../entities/definitions/context-entry.entity';
import { StartNodeEntity } from '../../../entities/definitions/start-node.entity';
import { ToastService } from '../../../services/toast.service';
import { RunSortField } from '../../../enumerations/run-sort-field';
import { SortDirection } from '../../../enumerations/sort-direction';
import { AssetSummary } from '../../assets/interfaces/asset-summary';
import { AssetScope } from '../../../enumerations/asset-scope';
import { AssetSortField } from '../../../enumerations/asset-sort-field';
import { ConversationHistoryTurnModel } from '../interfaces/conversation-history-turn-model';
import { ConversationHistoryModel } from '../interfaces/conversation-history-model';

@Injectable({
  providedIn: 'root',
})
export class WorkflowService implements OnDestroy {
  private readonly serverWorkflowService = inject(ServerRepositoryService);
  private readonly toastService = inject(ToastService);
  public readonly signalrService = inject(SignalrService);
  public readonly runsPageSize = 50;

  public workflow: WritableSignal<WorkflowEntity>;
  public runProgress: WritableSignal<RunProgressModel | undefined>;
  public traces: WritableSignal<TraceProgressModel[]>;
  public informations: WritableSignal<InformationProgressModel[]>;
  public runAssets: WritableSignal<AssetSummary[]>;
  public conversationTurns: WritableSignal<ConversationHistoryTurnModel[]>;
  public conversationAssets: WritableSignal<AssetSummary[]>;
  public runs: WritableSignal<RunProgressModel[]>;
  public runsTotal: WritableSignal<number>;
  public runsPage: WritableSignal<number>;
  public runsSortField: WritableSignal<RunSortField>;
  public runsSortDirection: WritableSignal<SortDirection>;
  public isRunning: WritableSignal<boolean>;
  public runInputs: WritableSignal<ContextEntryListEntity>;
  private lastStartInputsSnapshot?: ContextEntryListSnapshot;
  private activeLiveRunId?: string;
  private activeConversationId?: string;

  // Create a stable references for listener functions, so they run in correct zone
  private readonly runProgressListener = (data: RunProgressModel) =>
    this.onRunProgress(data);
  private readonly traceProgressListener = (data: TraceProgressModel) =>
    this.onTraceProgress(data);
  private readonly informationsProgressListener = (
    data: InformationProgressModel[],
  ) => this.onInformationsProgress(data);

  constructor() {
    this.workflow = signal(
      new WorkflowEntity(WorkflowEntity.defaultSnapshot()),
    );
    this.runProgress = signal(undefined);
    this.traces = signal([]);
    this.informations = signal([]);
    this.runAssets = signal([]);
    this.conversationTurns = signal([]);
    this.conversationAssets = signal([]);
    this.runs = signal([]);
    this.runsTotal = signal(0);
    this.runsPage = signal(1);
    this.runsSortField = signal(RunSortField.Created);
    this.runsSortDirection = signal(SortDirection.Descending);
    this.isRunning = signal(false);
    this.runInputs = signal(
      ContextEntryListEntity.fromSnapshot(
        ContextEntryListEntity.defaultSnapshot(),
      ),
    );

    effect(() => {
      if (this.signalrService.isConnected()) {
        this.addListeners();
      } else {
        this.removeListeners();
      }
    });

    effect(() => {
      const workflow = this.workflow();
      const startNode = workflow
        .nodes()
        .find(
          (node): node is StartNodeEntity => node instanceof StartNodeEntity,
        );
      const snapshot = startNode
        ? startNode.initializing().toSnapshot()
        : undefined;
      this.syncRunInputsWithStartSnapshot(snapshot);
    });
  }

  ngOnDestroy(): void {
    this.removeListeners();
    this.markClean();
  }

  load(id: string) {
    this.serverWorkflowService.getWorkflow(id).subscribe((workflow) => {
      if (!workflow) {
        return;
      }

      this.workflow.set(workflow);
      this.workflow()
        .nodes()
        .forEach((nodeEntity) => nodeEntity.displayState.set(NodeStatus.None));
      this.runProgress.set(undefined);
      this.traces.set([]);
      this.informations.set([]);
      this.runsTotal.set(0);
      this.runsPage.set(1);
      this.runsSortField.set(RunSortField.Created);
      this.runsSortDirection.set(SortDirection.Descending);
      this.runAssets.set([]);
      this.conversationTurns.set([]);
      this.conversationAssets.set([]);
      this.activeLiveRunId = undefined;
      this.activeConversationId = undefined;
      this.updateRunInputsFromWorkflow();
      this.workflow().markClean();
      this.loadRunsPageForWorkflow(id, 1);
      this.loadLatestExecutionState(id, workflow.isConversationEnabled());
    });
  }

  save(): Observable<void> {
    return this.serverWorkflowService.upsertWorkflow(this.workflow()).pipe(
      map(() => {
        this.workflow().markClean();
        return;
      }),
    );
  }

  run(): Observable<string | undefined> {
    this.isRunning.set(true);
    const workflow = this.workflow();
    const runRequest = workflow.isConversationEnabled()
      ? this.startNewConversationRun(workflow.id)
      : this.serverWorkflowService.runWorkflow(workflow.id, this.runInputs());

    return runRequest.pipe(
      map((runId) => {
        if (!runId) {
          this.isRunning.set(false);
          return undefined;
        }

        this.activeLiveRunId = runId;
        if (workflow.isConversationEnabled()) {
          this.prepareConversationLiveRun();
        } else {
          this.resetLiveRunState();
        }
        return runId;
      }),
    );
  }

  resumeConversation(
    resumeContextJson?: string,
  ): Observable<string | undefined> {
    const workflow = this.workflow();
    const conversationId =
      this.activeConversationId ?? this.runProgress()?.conversationId ?? undefined;

    if (
      !workflow.isConversationEnabled() ||
      !conversationId ||
      (this.runProgress()?.runStatus !== RunStatus.Suspended &&
        this.runProgress()?.runStatus !== RunStatus.Success)
    ) {
      this.toastService.error('No resumable conversation is available.');
      return of(undefined);
    }

    this.isRunning.set(true);

    return this.serverWorkflowService
      .resumeConversationWorkflow(
        workflow.id,
        conversationId,
        resumeContextJson,
      )
      .pipe(
        map((runId) => {
          if (!runId) {
            this.isRunning.set(false);
            return undefined;
          }

          this.activeConversationId = conversationId;
          this.activeLiveRunId = runId;
          this.resetNodeDisplayStates();
          return runId;
        }),
      );
  }

  markClean(): void {
    this.workflow().markClean();
    this.runInputs().markClean();
  }

  addListeners(): void {
    this.signalrService.addListener('RunProgress', this.runProgressListener);
    this.signalrService.addListener(
      'TraceProgress',
      this.traceProgressListener,
    );
    this.signalrService.addListener(
      'InformationsProgress',
      this.informationsProgressListener,
    );
  }

  removeListeners(): void {
    this.signalrService.removeListener('RunProgress', this.runProgressListener);
    this.signalrService.removeListener(
      'TraceProgress',
      this.traceProgressListener,
    );
    this.signalrService.removeListener(
      'InformationsProgress',
      this.informationsProgressListener,
    );
  }

  onRunProgress(data: RunProgressModel) {
    const workflow = this.workflow();
    if (!workflow || !this.shouldHandleRunProgress(data)) {
      return;
    }

    if (workflow.isConversationEnabled() && data.conversationId) {
      this.activeConversationId = data.conversationId;
    }

    switch (data.runStatus) {
      case RunStatus.Created: {
        this.resetNodeDisplayStates();
        this.runProgress.set(data);
        if (workflow.isConversationEnabled()) {
          this.upsertConversationTurnRun(data);
          this.syncLatestTurnSignals(data.runId);
        } else {
          this.traces.set([]);
          this.informations.set([]);
          this.runAssets.set([]);
        }
        break;
      }
      case RunStatus.Running: {
        this.runProgress.set(data);
        if (workflow.isConversationEnabled()) {
          this.upsertConversationTurnRun(data);
          this.syncLatestTurnSignals(data.runId);
        }
        break;
      }
      case RunStatus.Suspended: {
        this.runProgress.set(data);
        this.isRunning.set(false);
        this.activeLiveRunId = undefined;
        this.toastService.success(`${workflow.name()} suspended.`);
        if (workflow.isConversationEnabled() && data.conversationId) {
          this.upsertConversationTurnRun(data);
          this.loadConversationHistory(data.conversationId);
        } else {
          this.updateRunAssetsForRun(data);
        }
        break;
      }
      case RunStatus.Success: {
        this.runProgress.set(data);
        this.isRunning.set(false);
        this.activeLiveRunId = undefined;
        const workflowName = workflow.name();
        const successMessage = `${workflowName} completed successfully.`;
        this.toastService.success(successMessage);
        if (workflow.isConversationEnabled() && data.conversationId) {
          this.upsertConversationTurnRun(data);
          this.loadConversationHistory(data.conversationId);
        } else {
          this.updateRunAssetsForRun(data);
        }
        break;
      }
      case RunStatus.Failed: {
        this.runProgress.set(data);
        this.isRunning.set(false);
        this.activeLiveRunId = undefined;
        const workflowName = workflow.name();
        const errorMessage = (data.error ?? '').trim();
        const failureMessage = errorMessage
          ? `${workflowName} failed: ${errorMessage}`
          : `${workflowName} failed.`;
        this.toastService.error(failureMessage);
        if (workflow.isConversationEnabled() && data.conversationId) {
          this.upsertConversationTurnRun(data);
          this.loadConversationHistory(data.conversationId);
        } else {
          this.updateRunAssetsForRun(data);
        }
        break;
      }
    }
  }

  onTraceProgress(data: TraceProgressModel) {
    const workflow = this.workflow();
    if (!workflow || !this.shouldHandleTraceProgress(data)) {
      return;
    }

    const nodeEntity = workflow
      .nodes()
      .find((n) => n.id == data.nodeEntityId);
    if (nodeEntity) {
      nodeEntity.displayState.set(data.nodeStatus);
    }
    if (workflow.isConversationEnabled()) {
      this.upsertConversationTurnTrace(data);
      this.syncLatestTurnSignals(data.runId);
      return;
    }

    if (data.nodeStatus === NodeStatus.Running) {
      this.traces.update((traces) => [...traces, data]);
    } else {
      const traces = this.traces();
      const idx = traces.findIndex((t) => t.traceId === data.traceId);
      if (idx >= 0) {
        traces[idx] = data;
        this.traces.set([...traces]);
      } else {
        this.traces.update((currentTraces) => [...currentTraces, data]);
      }
    }
  }

  onInformationsProgress(data: InformationProgressModel[]) {
    if (!data || data.length === 0) {
      return;
    }

    const currentRunId = this.activeLiveRunId;
    if (!currentRunId) {
      return;
    }

    const matchingInformations = data.filter(
      (information) => information.runId === currentRunId,
    );
    if (matchingInformations.length === 0) {
      return;
    }

    if (this.workflow().isConversationEnabled()) {
      this.upsertConversationTurnInformations(currentRunId, matchingInformations);
      this.syncLatestTurnSignals(currentRunId);
      return;
    }

    this.informations.update((informations) => {
      const byId = new Map(
        informations.map((information) => [information.informationId, information]),
      );
      matchingInformations.forEach((information) =>
        byId.set(information.informationId, information),
      );
      return this.sortInformations([...byId.values()]);
    });
  }

  private updateRunInputsFromWorkflow(): void {
    const workflow = this.workflow();
    const startNode = workflow
      .nodes()
      .find((node): node is StartNodeEntity => node instanceof StartNodeEntity);

    if (startNode) {
      const snapshot = startNode.initializing().toSnapshot();
      this.runInputs.set(ContextEntryListEntity.fromSnapshot(snapshot));
    } else {
      this.runInputs.set(
        ContextEntryListEntity.fromSnapshot(
          ContextEntryListEntity.defaultSnapshot(),
        ),
      );
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
      if (
        parsed &&
        typeof parsed === 'object' &&
        Array.isArray(parsed.entries)
      ) {
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
    previousInputs.entries().forEach((entry) => {
      previousEntriesByPath.set(entry.inputPath(), entry);
    });

    this.runInputs()
      .entries()
      .forEach((entry) => {
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

    const normalizedPage = Number.isFinite(page)
      ? Math.max(1, Math.floor(page))
      : 1;
    this.loadRunsPageForWorkflow(workflowId, normalizedPage);
  }

  public updateRunsSort(field: RunSortField): void {
    const workflowId = this.workflow().id;
    if (this.runsSortField() === field) {
      const nextDirection =
        this.runsSortDirection() === SortDirection.Descending
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
    this.serverWorkflowService
      .getLatestWorkflowRuns(
        workflowId,
        page,
        this.runsPageSize,
        sortBy,
        sortDirection,
      )
      .subscribe((result) => {
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

  private syncRunInputsWithStartSnapshot(
    snapshot?: ContextEntryListSnapshot,
  ): void {
    if (!snapshot) {
      this.lastStartInputsSnapshot = undefined;
      return;
    }

    const previousSnapshot = this.lastStartInputsSnapshot;

    untracked(() => {
      const runInputs = this.runInputs();
      const currentEntries = runInputs.entries();
      const currentEntriesByPath = new Map<string, ContextEntryEntity>();
      currentEntries.forEach((entry) =>
        currentEntriesByPath.set(entry.inputPath(), entry),
      );
      const previousEntriesByPath = new Map<string, ContextEntrySnapshot>();
      previousSnapshot?.entries.forEach((entry) =>
        previousEntriesByPath.set(entry.inputPath, entry),
      );

      let needsUpdate = currentEntries.length !== snapshot.entries.length;
      const nextEntries: ContextEntryEntity[] = [];

      snapshot.entries.forEach((entrySnapshot, index) => {
        const existingEntry = currentEntriesByPath.get(entrySnapshot.inputPath);
        const previousEntrySnapshot = previousEntriesByPath.get(
          entrySnapshot.inputPath,
        );
        let entry: ContextEntryEntity;

        if (existingEntry) {
          entry = existingEntry;
          currentEntriesByPath.delete(entrySnapshot.inputPath);
        } else {
          entry = ContextEntryEntity.fromSnapshot(entrySnapshot);
          needsUpdate = true;
        }

        const entryUpdated = this.applyEntrySnapshot(
          entry,
          entrySnapshot,
          previousEntrySnapshot,
        );
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

  private applyEntrySnapshot(
    entry: ContextEntryEntity,
    snapshot: ContextEntrySnapshot,
    previousSnapshot?: ContextEntrySnapshot,
  ): boolean {
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

    if (
      run.runStatus !== RunStatus.Success &&
      run.runStatus !== RunStatus.Suspended &&
      run.runStatus !== RunStatus.Failed
    ) {
      this.runAssets.set([]);
      return;
    }

    this.serverWorkflowService
      .getAssets(
        AssetScope.Run,
        0,
        0,
        AssetSortField.Created,
        SortDirection.Descending,
        '',
        run.runId,
      )
      .subscribe((assets) => {
        this.runAssets.set(assets ?? []);
      });
  }

  private prepareConversationLiveRun(): void {
    this.resetNodeDisplayStates();
    const runId = this.activeLiveRunId;
    if (!runId) {
      return;
    }

    this.conversationTurns.update((turns) =>
      turns.map((turn) =>
        turn.run.runId === runId
          ? { ...turn, traces: [], informations: [], assets: [] }
          : turn,
      ),
    );
  }

  private upsertConversationTurnRun(run: RunProgressModel): void {
    this.conversationTurns.update((turns) => {
      const index = turns.findIndex((turn) => turn.run.runId === run.runId);
      const nextTurns = [...turns];
      if (index >= 0) {
        nextTurns[index] = { ...nextTurns[index], run };
      } else {
        nextTurns.push({ run, traces: [], informations: [], assets: [] });
      }

      return this.sortConversationTurns(nextTurns);
    });
  }

  private upsertConversationTurnTrace(trace: TraceProgressModel): void {
    this.conversationTurns.update((turns) =>
      turns.map((turn) => {
        if (turn.run.runId !== trace.runId) {
          return turn;
        }

        if (trace.nodeStatus === NodeStatus.Running) {
          return { ...turn, traces: [...turn.traces, trace] };
        }

        const traces = [...turn.traces];
        const index = traces.findIndex((entry) => entry.traceId === trace.traceId);
        if (index >= 0) {
          traces[index] = trace;
        } else {
          traces.push(trace);
        }

        return { ...turn, traces };
      }),
    );
  }

  private upsertConversationTurnInformations(
    runId: string,
    informations: InformationProgressModel[],
  ): void {
    this.conversationTurns.update((turns) =>
      turns.map((turn) => {
        if (turn.run.runId !== runId) {
          return turn;
        }

        const byId = new Map(
          turn.informations.map((information) => [information.informationId, information]),
        );
        informations.forEach((information) =>
          byId.set(information.informationId, information),
        );

        return {
          ...turn,
          informations: this.sortInformations([...byId.values()]),
        };
      }),
    );
  }

  private sortConversationTurns(
    turns: ConversationHistoryTurnModel[],
  ): ConversationHistoryTurnModel[] {
    return [...turns]
      .map((turn) => ({
        ...turn,
        traces: [...(turn.traces ?? [])],
        informations: this.sortInformations(turn.informations ?? []),
        assets: this.sortAssetsByCreated(turn.assets ?? []),
      }))
      .sort((left, right) => {
        const leftTurn = left.run.turnNumber ?? 0;
        const rightTurn = right.run.turnNumber ?? 0;
        if (leftTurn !== rightTurn) {
          return leftTurn - rightTurn;
        }

        const leftTime = Date.parse(left.run.created);
        const rightTime = Date.parse(right.run.created);
        const normalizedLeft = Number.isFinite(leftTime) ? leftTime : 0;
        const normalizedRight = Number.isFinite(rightTime) ? rightTime : 0;
        return normalizedLeft - normalizedRight;
      });
  }

  private sortAssetsByCreated(assets: AssetSummary[]): AssetSummary[] {
    return [...assets].sort((left, right) => {
      const leftTime = Date.parse(left.created);
      const rightTime = Date.parse(right.created);
      const normalizedLeft = Number.isFinite(leftTime) ? leftTime : 0;
      const normalizedRight = Number.isFinite(rightTime) ? rightTime : 0;
      return normalizedLeft - normalizedRight;
    });
  }

  private getTurnTraces(runId: string): TraceProgressModel[] {
    return (
      this.conversationTurns().find((turn) => turn.run.runId === runId)?.traces ?? []
    );
  }

  private getTurnInformations(runId: string): InformationProgressModel[] {
    return (
      this.conversationTurns().find((turn) => turn.run.runId === runId)?.informations ??
      []
    );
  }

  private getTurnAssets(runId: string): AssetSummary[] {
    return (
      this.conversationTurns().find((turn) => turn.run.runId === runId)?.assets ?? []
    );
  }

  private syncLatestTurnSignals(runId: string): void {
    this.traces.set([...this.getTurnTraces(runId)]);
    this.informations.set(this.sortInformations(this.getTurnInformations(runId)));
    this.runAssets.set([...this.getTurnAssets(runId)]);
  }

  private sortInformations(
    informations: InformationProgressModel[],
  ): InformationProgressModel[] {
    return [...informations].sort((left, right) => {
      const leftTime = Date.parse(left.created);
      const rightTime = Date.parse(right.created);
      const normalizedLeft = Number.isFinite(leftTime) ? leftTime : 0;
      const normalizedRight = Number.isFinite(rightTime) ? rightTime : 0;
      return normalizedLeft - normalizedRight;
    });
  }

  private loadLatestExecutionState(
    workflowId: string,
    isConversationEnabled: boolean,
  ): void {
    if (isConversationEnabled) {
      this.serverWorkflowService
        .getLatestWorkflowConversation(workflowId)
        .subscribe((conversation) => {
          if (!conversation?.conversationId) {
            this.clearLatestExecutionState();
            return;
          }

          this.loadConversationHistory(conversation.conversationId);
        });
      return;
    }

    this.serverWorkflowService.getLatestWorkflowRun(workflowId).subscribe((run) => {
      if (run) {
        this.applySingleRunState(run);
        return;
      }

      this.clearLatestExecutionState();
    });
  }

  private loadConversationHistory(conversationId: string): void {
    this.serverWorkflowService
      .getConversationHistory(conversationId)
      .subscribe((history) => {
        if (!history) {
          this.clearLatestExecutionState();
          return;
        }

        this.activeConversationId = history.conversationId;
        const sortedTurns = this.sortConversationTurns(history.turns ?? []);
        this.conversationTurns.set(sortedTurns);
        this.conversationAssets.set(
          this.sortAssetsByCreated(history.conversationAssets ?? []),
        );

        const latestRun = history.latestRun ?? sortedTurns.at(-1)?.run;
        if (!latestRun) {
          this.clearLatestExecutionState();
          this.activeConversationId = conversationId;
          this.conversationTurns.set(sortedTurns);
          this.conversationAssets.set(
            this.sortAssetsByCreated(history.conversationAssets ?? []),
          );
          return;
        }

        this.runProgress.set(latestRun);
        this.activeLiveRunId = this.isLiveRun(latestRun) ? latestRun.runId : undefined;
        this.updateRunInputsFromLatestRun();
        this.syncLatestTurnSignals(latestRun.runId);
        this.applyNodeDisplayStates(this.getTurnTraces(latestRun.runId));
      });
  }

  private applySingleRunState(run: RunProgressModel): void {
    this.runProgress.set(run);
    this.activeLiveRunId = this.isLiveRun(run) ? run.runId : undefined;
    this.activeConversationId = run.conversationId ?? undefined;
    this.conversationTurns.set([]);
    this.conversationAssets.set([]);
    this.updateRunInputsFromLatestRun();
    this.serverWorkflowService.getRunTraces(run.runId).subscribe((traces) => {
      const nextTraces = traces ?? [];
      this.traces.set(nextTraces);
      this.applyNodeDisplayStates(nextTraces);
    });
    this.serverWorkflowService
      .getRunInformations(run.runId)
      .subscribe((informations) => {
        this.informations.set(this.sortInformations(informations ?? []));
      });
    this.updateRunAssetsForRun(run);
  }

  private clearLatestExecutionState(): void {
    this.activeLiveRunId = undefined;
    this.activeConversationId = undefined;
    this.runProgress.set(undefined);
    this.traces.set([]);
    this.informations.set([]);
    this.runAssets.set([]);
    this.conversationTurns.set([]);
    this.conversationAssets.set([]);
    this.resetNodeDisplayStates();
  }

  private startNewConversationRun(
    workflowId: string,
  ): Observable<string | undefined> {
    const conversationId = crypto.randomUUID();
    this.clearLatestExecutionState();
    this.activeConversationId = conversationId;

    return this.serverWorkflowService.runConversationWorkflow(
      workflowId,
      conversationId,
      this.runInputs(),
    );
  }

  private shouldHandleRunProgress(data: RunProgressModel): boolean {
    if (data.workflowId !== this.workflow().id) {
      return false;
    }

    if (this.activeLiveRunId) {
      return data.runId === this.activeLiveRunId;
    }

    if (
      this.runProgress()?.runId === data.runId &&
      this.isLiveRun(data)
    ) {
      this.activeLiveRunId = data.runId;
      return true;
    }

    return false;
  }

  private shouldHandleTraceProgress(data: TraceProgressModel): boolean {
    return (
      data.workflowId === this.workflow().id &&
      !!this.activeLiveRunId &&
      data.runId === this.activeLiveRunId
    );
  }

  private isLiveRun(run: RunProgressModel): boolean {
    return (
      run.needsEditorEvents &&
      (run.runStatus === RunStatus.Created || run.runStatus === RunStatus.Running)
    );
  }

  private resetLiveRunState(): void {
    this.resetNodeDisplayStates();
    this.runProgress.set(undefined);
    this.traces.set([]);
    this.informations.set([]);
    this.runAssets.set([]);
  }

  private resetNodeDisplayStates(): void {
    this.workflow()
      .nodes()
      .forEach((nodeEntity) => nodeEntity.displayState.set(NodeStatus.None));
  }

  private applyNodeDisplayStates(traces: TraceProgressModel[]): void {
    this.resetNodeDisplayStates();
    const nodes = this.workflow().nodes();
    traces.forEach((trace) => {
      const nodeEntity = nodes.find((node) => node.id == trace.nodeEntityId);
      if (nodeEntity) {
        nodeEntity.displayState.set(trace.nodeStatus);
      }
    });
  }

}
