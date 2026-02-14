import { CommonModule } from '@angular/common';
import {
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  TemplateRef,
  ViewChild,
  inject,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, forkJoin, map, of, switchMap, take } from 'rxjs';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { EvalConfig } from '../../eval/definitions/eval-config';
import { EvalColumn } from '../../eval/definitions/eval-column';
import { EvalConfigDetailSnapshot } from '../../eval/definitions/eval-config-detail';
import { EvalDataSnapshot } from '../../eval/definitions/eval-data';
import { EvalGrader } from '../../eval/definitions/eval-grader';
import { EvalRow } from '../../eval/definitions/eval-row';
import { CanLeaveWithUnsavedChanges } from '../../helper/unsaved-changes.guard';
import { ContextEntryType } from '../../entities/enumerations/context-entry-type';
import { WorkflowSummaryEntity } from '../../entities/definitions/workflow.summary.entity';
import { WorkflowSortField } from '../../enumerations/workflow-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';
import { MonacoService } from '../../services/monaco.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { EvalStartRunDialogComponent } from '../../dialogs/eval-start-run/eval-start-run-dialog.component';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { EvalRunStatus } from '../../eval/enumerations/eval-run-status';
import { EvalRunSortField } from '../../eval/enumerations/eval-run-sort-field';
import { EvalRunSummarySnapshot } from '../../eval/definitions/eval-run-summary';
import {
  EvalRunDetailSnapshot,
  EvalRunGraderSummaryDetailSnapshot,
} from '../../eval/definitions/eval-run-detail';

@Component({
  selector: 'app-evaluation',
  standalone: true,
  imports: [CommonModule, FormsModule, TabComponent, MonacoEditorModule],
  templateUrl: './evaluation.component.html',
  styleUrls: ['./evaluation.component.scss'],
  providers: [BsModalService],
})
export class EvaluationComponent
  implements OnInit, OnDestroy, CanLeaveWithUnsavedChanges
{
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('columnsTab', { static: true }) columnsTab!: TemplateRef<unknown>;
  @ViewChild('rowsTab', { static: true }) rowsTab!: TemplateRef<unknown>;
  @ViewChild('gradersTab', { static: true }) gradersTab!: TemplateRef<unknown>;
  @ViewChild('runsTab', { static: true }) runsTab!: TemplateRef<unknown>;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private startRunModalRef: BsModalRef<EvalStartRunDialogComponent> | undefined;
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;

  public evalConfig = EvalConfig.fromSnapshot(EvalConfig.defaultSnapshot());
  public readonly contextEntryType = ContextEntryType;
  public readonly columnTypeKeys = Object.keys(ContextEntryType).filter(
    (key) =>
      isNaN(Number(key)) && this.isAllowedColumnType(this.getEnumValue(key)),
  );
  public readonly evalRunStatus = EvalRunStatus;
  private readonly tabIds = new Set([
    'details',
    'columns',
    'rows',
    'graders',
    'runs',
  ]);
  private readonly defaultTabId = 'details';
  private readonly runsPageSize = 20;
  private hasLoadedConfig = false;
  private hasLoadedWorkflows = false;
  private normalizedWorkflowSelection = false;
  private runsLoaded = false;
  private runSearchDebounceId: ReturnType<typeof setTimeout> | undefined;
  public workflowSummaries: WorkflowSummaryEntity[] = [];
  public tabs: TabItem[] = [];
  public activeTabId = this.defaultTabId;
  public selectedRowId: string | null = null;
  public isStartingRun = false;
  public runs: EvalRunSummarySnapshot[] = [];
  public runsTotal = 0;
  public runsPage = 1;
  public runsSearchText = '';
  public selectedRunId: string | null = null;
  public selectedRunDetail: EvalRunDetailSnapshot | null = null;
  public isLoadingRuns = false;
  public isLoadingRunDetail = false;
  public isDeletingRun = false;

  ngOnInit(): void {
    this.tabs = [
      { id: 'details', title: 'Details', content: this.detailsTab },
      { id: 'columns', title: 'Columns', content: this.columnsTab },
      { id: 'rows', title: 'Rows', content: this.rowsTab },
      { id: 'graders', title: 'Graders', content: this.gradersTab },
      { id: 'runs', title: 'Runs', content: this.runsTab },
    ];

    this.activeTabId = this.resolveTabId(
      this.route.snapshot.queryParamMap.get('tab'),
    );
    this.route.queryParamMap.subscribe((params) => {
      const nextTabId = this.resolveTabId(params.get('tab'));
      if (nextTabId !== this.activeTabId) {
        this.activeTabId = nextTabId;
      }

      if (nextTabId === 'runs') {
        this.ensureRunsLoaded();
      }
    });
    this.evalConfig.ensureRequiredNameColumn();
    this.ensureSelectedRow();

    this.loadWorkflows();

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadEvalConfig(id);
    } else {
      this.hasLoadedConfig = true;
      if (this.activeTabId === 'runs') {
        this.ensureRunsLoaded();
      }
    }
  }

  ngOnDestroy(): void {
    if (this.runSearchDebounceId) {
      clearTimeout(this.runSearchDebounceId);
      this.runSearchDebounceId = undefined;
    }
  }

  save(): void {
    this.saveChanges().subscribe();
  }

  canStartRun(): boolean {
    return (
      !this.isStartingRun &&
      !this.evalConfig.isDirty() &&
      !this.hasColumnValidationErrors() &&
      !this.hasRowValidationErrors()
    );
  }

  startRun(): void {
    const rowCount = this.evalConfig.rows().length;
    const graderCount = this.evalConfig.graders().length;
    const rowLabel = rowCount === 1 ? 'evaluation' : 'evaluations';
    const graderLabel = graderCount === 1 ? 'grader' : 'graders';

    this.startRunModalRef = this.modalService.show(EvalStartRunDialogComponent, {
      initialState: {
        title: 'Start Evaluation Run',
        message: `Are you sure you want to run this evaluation?\n\nThis will execute ${rowCount} ${rowLabel} with ${graderCount} ${graderLabel}.`,
        rowCount,
      },
    });

    const modalRef = this.startRunModalRef;
    modalRef.onHidden?.pipe(take(1)).subscribe(() => {
      if (!modalRef.content?.result || this.isStartingRun) {
        return;
      }

      this.isStartingRun = true;
      const runName = modalRef.content.runName;
      const sampleCount = modalRef.content.getSelectedSampleCount();
      this.serverRepository
        .startEvalRun(this.evalConfig.evalConfigId, runName, sampleCount)
        .subscribe({
          next: (evalRunId) => {
            this.isStartingRun = false;
            if (evalRunId) {
              this.activeTabId = 'runs';
              this.updateTabRoute('runs');
              this.refreshRuns(evalRunId);
            }
          },
        });
    });
  }

  onActiveTabChange(tab: TabItem): void {
    this.updateTabRoute(tab.id);
    if (tab.id === 'runs') {
      this.ensureRunsLoaded();
    }
  }

  onWorkflowChange(value: string | null): void {
    this.evalConfig.workflowId.set(value ?? null);
  }

  onGraderWorkflowChange(grader: EvalGrader, value: string | null): void {
    grader.workflowId.set(value ?? null);
  }

  onMaxParallelChange(value: string | number): void {
    const numeric = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(numeric)) {
      this.evalConfig.maxParallel.set(1);
      return;
    }

    this.evalConfig.maxParallel.set(Math.max(1, Math.trunc(numeric)));
  }

  onPassThresholdChange(grader: EvalGrader, value: string | number): void {
    const numeric = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(numeric)) {
      grader.passThreshold.set(0);
      return;
    }

    grader.passThreshold.set(numeric);
  }

  getEnumValue(key: string): ContextEntryType {
    return this.contextEntryType[
      key as keyof typeof ContextEntryType
    ] as ContextEntryType;
  }

  getEnumDisplay(key: string): string {
    switch (key) {
      case 'Expression':
        return '(expression)';
      case 'JSON':
        return '(json)';
      default:
        return key.toLowerCase();
    }
  }

  get workflowSelectionId(): string | null {
    const workflowId = this.evalConfig.workflowId();
    if (!workflowId) {
      return null;
    }

    const exists = this.workflowSummaries.some(
      (workflow) => workflow.id === workflowId,
    );
    return exists ? workflowId : null;
  }

  graderWorkflowSelectionId(grader: EvalGrader): string | null {
    const workflowId = grader.workflowId();
    if (!workflowId) {
      return null;
    }

    const exists = this.workflowSummaries.some(
      (workflow) => workflow.id === workflowId,
    );
    return exists ? workflowId : null;
  }

  appendGrader(): void {
    const graders = this.evalConfig.graders();
    const snapshot = EvalGrader.defaultSnapshot(
      graders.length,
      this.evalConfig.evalConfigId,
    );
    const next = [...graders, EvalGrader.fromSnapshot(snapshot)];
    this.evalConfig.graders.set(next);
  }

  canMoveGraderUp(grader: EvalGrader): boolean {
    const graders = this.evalConfig.graders();
    return graders.indexOf(grader) > 0;
  }

  canMoveGraderDown(grader: EvalGrader): boolean {
    const graders = this.evalConfig.graders();
    const index = graders.indexOf(grader);
    return index >= 0 && index < graders.length - 1;
  }

  onMoveGraderUp(grader: EvalGrader): void {
    const graders = this.evalConfig.graders();
    const index = graders.indexOf(grader);
    if (index <= 0) {
      return;
    }

    const next = graders.slice();
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    this.evalConfig.graders.set(next);
  }

  onMoveGraderDown(grader: EvalGrader): void {
    const graders = this.evalConfig.graders();
    const index = graders.indexOf(grader);
    if (index < 0 || index >= graders.length - 1) {
      return;
    }

    const next = graders.slice();
    [next[index], next[index + 1]] = [next[index + 1], next[index]];
    this.evalConfig.graders.set(next);
  }

  onDeleteGrader(grader: EvalGrader): void {
    const graders = this.evalConfig.graders();
    const index = graders.indexOf(grader);
    if (index < 0) {
      return;
    }

    const next = graders.slice();
    next.splice(index, 1);
    this.evalConfig.graders.set(next);
  }

  appendColumn(): void {
    const columns = this.evalConfig.columns();
    const snapshot = EvalColumn.defaultSnapshot(
      columns.length,
      this.evalConfig.evalConfigId,
    );
    const newColumn = EvalColumn.fromSnapshot(snapshot);
    const next = [...columns, newColumn];
    this.evalConfig.columns.set(next);
    this.seedMissingDataForColumn(newColumn);
  }

  isFixedColumn(column: EvalColumn): boolean {
    const columns = this.evalConfig.columns();
    return columns.indexOf(column) === 0;
  }

  canMoveColumnUp(column: EvalColumn): boolean {
    if (this.isFixedColumn(column)) {
      return false;
    }

    const columns = this.evalConfig.columns();
    return columns.indexOf(column) > 1;
  }

  canMoveColumnDown(column: EvalColumn): boolean {
    if (this.isFixedColumn(column)) {
      return false;
    }

    const columns = this.evalConfig.columns();
    const index = columns.indexOf(column);
    return index >= 1 && index < columns.length - 1;
  }

  onMoveColumnUp(column: EvalColumn): void {
    const columns = this.evalConfig.columns();
    const index = columns.indexOf(column);
    if (index <= 1) {
      return;
    }

    const next = columns.slice();
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    this.evalConfig.columns.set(next);
  }

  onMoveColumnDown(column: EvalColumn): void {
    const columns = this.evalConfig.columns();
    const index = columns.indexOf(column);
    if (index < 1 || index >= columns.length - 1) {
      return;
    }

    const next = columns.slice();
    [next[index], next[index + 1]] = [next[index + 1], next[index]];
    this.evalConfig.columns.set(next);
  }

  onDeleteColumn(column: EvalColumn): void {
    if (this.isFixedColumn(column)) {
      return;
    }

    const columns = this.evalConfig.columns();
    const index = columns.indexOf(column);
    if (index < 0) {
      return;
    }

    const next = columns.slice();
    next.splice(index, 1);
    this.evalConfig.columns.set(next);
  }

  onColumnNameChange(column: EvalColumn, value: string): void {
    if (this.isFixedColumn(column)) {
      column.name.set(EvalConfig.REQUIRED_NAME_COLUMN_NAME);
      return;
    }

    column.name.set(value ?? '');
  }

  onColumnTypeChange(column: EvalColumn, value: string | number): void {
    if (this.isFixedColumn(column)) {
      column.entryType.set(ContextEntryType.String);
      return;
    }

    const numeric = Number(value);
    if (!Number.isFinite(numeric)) {
      column.entryType.set(ContextEntryType.String);
      return;
    }

    const entryType = numeric as ContextEntryType;
    if (!this.isAllowedColumnType(entryType)) {
      column.entryType.set(ContextEntryType.String);
      return;
    }

    column.entryType.set(entryType);
  }

  onColumnOptionalChange(column: EvalColumn, value: boolean): void {
    if (this.isFixedColumn(column)) {
      column.optional.set(false);
      return;
    }

    column.optional.set(Boolean(value));
  }

  onColumnInputPathChange(column: EvalColumn, value: string): void {
    if (this.isFixedColumn(column)) {
      column.inputPath.set(null);
      return;
    }

    const trimmed = value?.trim() ?? '';
    column.inputPath.set(trimmed.length ? trimmed : null);
  }

  get selectedRow(): EvalRow | null {
    const rows = this.evalConfig.rows();
    if (!this.selectedRowId) {
      return null;
    }

    return rows.find((row) => row.evalRowId === this.selectedRowId) ?? null;
  }

  selectRow(row: EvalRow): void {
    this.selectedRowId = row.evalRowId;
  }

  isRowSelected(row: EvalRow): boolean {
    return row.evalRowId === this.selectedRowId;
  }

  appendRow(): void {
    const rows = this.evalConfig.rows();
    const snapshot = EvalRow.defaultSnapshot(
      rows.length,
      this.evalConfig.evalConfigId,
    );
    const newRow = EvalRow.fromSnapshot(snapshot);
    this.evalConfig.rows.set([...rows, newRow]);
    this.seedMissingDataForRow(newRow);
    this.selectedRowId = newRow.evalRowId;
  }

  canMoveRowUp(row: EvalRow): boolean {
    const rows = this.evalConfig.rows();
    return rows.indexOf(row) > 0;
  }

  canMoveRowDown(row: EvalRow): boolean {
    const rows = this.evalConfig.rows();
    const index = rows.indexOf(row);
    return index >= 0 && index < rows.length - 1;
  }

  onMoveRowUp(row: EvalRow): void {
    const rows = this.evalConfig.rows();
    const index = rows.indexOf(row);
    if (index <= 0) {
      return;
    }

    const next = rows.slice();
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    this.evalConfig.rows.set(next);
  }

  onMoveRowDown(row: EvalRow): void {
    const rows = this.evalConfig.rows();
    const index = rows.indexOf(row);
    if (index < 0 || index >= rows.length - 1) {
      return;
    }

    const next = rows.slice();
    [next[index], next[index + 1]] = [next[index + 1], next[index]];
    this.evalConfig.rows.set(next);
  }

  onDeleteRow(row: EvalRow): void {
    const rows = this.evalConfig.rows();
    const index = rows.indexOf(row);
    if (index < 0) {
      return;
    }

    const next = rows.slice();
    next.splice(index, 1);
    this.evalConfig.rows.set(next);
    if (this.selectedRowId === row.evalRowId) {
      const nextIndex = Math.min(index, next.length - 1);
      this.selectedRowId = next[nextIndex]?.evalRowId ?? null;
    }
    this.ensureSelectedRow();
  }

  canMoveSelectedRowUp(): boolean {
    return this.selectedRow !== null && this.canMoveRowUp(this.selectedRow);
  }

  canMoveSelectedRowDown(): boolean {
    return this.selectedRow !== null && this.canMoveRowDown(this.selectedRow);
  }

  onMoveSelectedRowUp(): void {
    if (!this.selectedRow) {
      return;
    }

    this.onMoveRowUp(this.selectedRow);
  }

  onMoveSelectedRowDown(): void {
    if (!this.selectedRow) {
      return;
    }

    this.onMoveRowDown(this.selectedRow);
  }

  onDeleteSelectedRow(): void {
    if (!this.selectedRow) {
      return;
    }

    this.onDeleteRow(this.selectedRow);
  }

  getRowListDisplayName(row: EvalRow, index: number): string {
    const name = this.getRowNameValue(row).trim();
    if (name.length > 0) {
      return name;
    }

    return `(unnamed row ${index + 1})`;
  }

  getRowNameValue(row: EvalRow): string {
    const nameColumn = this.getRequiredNameColumn();
    if (!nameColumn) {
      return '';
    }

    return this.getStringCellValue(row, nameColumn);
  }

  isRowNameInvalid(row: EvalRow): boolean {
    return this.getRowNameValue(row).trim().length === 0;
  }

  isRowNameFieldInvalid(row: EvalRow, column: EvalColumn): boolean {
    const nameColumn = this.getRequiredNameColumn();
    if (!nameColumn) {
      return false;
    }

    return (
      column.evalColumnId === nameColumn.evalColumnId && this.isRowNameInvalid(row)
    );
  }

  hasRowValidationErrors(): boolean {
    const rows = this.evalConfig.rows();
    if (!rows.length) {
      return false;
    }

    return rows.some((row) => this.isRowNameInvalid(row));
  }

  getStringCellValue(row: EvalRow, column: EvalColumn): string {
    return this.findCellSnapshot(row, column)?.stringValue ?? '';
  }

  onStringCellChange(row: EvalRow, column: EvalColumn, value: string): void {
    const snapshot = this.getWritableCellSnapshot(row, column);
    const normalized = value ?? '';
    snapshot.stringValue = normalized.length > 0 ? normalized : null;
    snapshot.intValue = null;
    snapshot.doubleValue = null;
    snapshot.boolValue = null;
    this.evalConfig.dataStore.upsert(snapshot);
  }

  onStringCellBlur(row: EvalRow, column: EvalColumn, rawValue: string): void {
    this.onStringCellChange(row, column, rawValue ?? '');
  }

  getNumericCellValue(row: EvalRow, column: EvalColumn): number | null {
    const snapshot = this.findCellSnapshot(row, column);
    if (!snapshot) {
      return null;
    }

    const entryType = column.entryType();
    if (entryType === ContextEntryType.Int) {
      return snapshot.intValue;
    }

    return snapshot.doubleValue;
  }

  onNumericCellChange(
    row: EvalRow,
    column: EvalColumn,
    value: string | number | null,
  ): void {
    this.setNumericCellValue(row, column, value);
  }

  onNumericCellBlur(
    row: EvalRow,
    column: EvalColumn,
    rawValue: string | number | null,
  ): void {
    this.setNumericCellValue(row, column, rawValue);
  }

  getBoolCellModelValue(row: EvalRow, column: EvalColumn): string {
    const snapshot = this.findCellSnapshot(row, column);
    if (!snapshot) {
      return '';
    }

    if (snapshot.boolValue === true) {
      return 'true';
    }

    if (snapshot.boolValue === false) {
      return 'false';
    }

    return '';
  }

  onBoolCellChange(row: EvalRow, column: EvalColumn, value: string): void {
    const snapshot = this.getWritableCellSnapshot(row, column);
    snapshot.boolValue =
      value === 'true' ? true : value === 'false' ? false : null;
    snapshot.stringValue = null;
    snapshot.intValue = null;
    snapshot.doubleValue = null;
    this.evalConfig.dataStore.upsert(snapshot);
  }

  getCellEditorOptions(column: EvalColumn): any {
    if (column.entryType() === ContextEntryType.JSON) {
      return MonacoService.editorOptionsJson;
    }

    return MonacoService.editorOptionsCSharp;
  }

  isJsonColumn(column: EvalColumn): boolean {
    return column.entryType() === ContextEntryType.JSON;
  }

  isExpressionColumn(column: EvalColumn): boolean {
    return column.entryType() === ContextEntryType.Expression;
  }

  isBooleanColumn(column: EvalColumn): boolean {
    return column.entryType() === ContextEntryType.Bool;
  }

  isIntColumn(column: EvalColumn): boolean {
    return column.entryType() === ContextEntryType.Int;
  }

  isDoubleColumn(column: EvalColumn): boolean {
    return column.entryType() === ContextEntryType.Double;
  }

  isColumnNameInvalid(column: EvalColumn): boolean {
    if (this.isFixedColumn(column)) {
      return false;
    }

    const name = column.name().trim();
    if (!name.length) {
      return true;
    }

    return (
      name.toLowerCase() === EvalConfig.REQUIRED_NAME_COLUMN_NAME.toLowerCase()
    );
  }

  getColumnNameValidationMessage(column: EvalColumn): string {
    if (!this.isColumnNameInvalid(column)) {
      return '';
    }

    const name = column.name().trim();
    if (!name.length) {
      return 'Column name is required.';
    }

    return `"${EvalConfig.REQUIRED_NAME_COLUMN_NAME}" is reserved for the fixed first column.`;
  }

  hasColumnValidationErrors(): boolean {
    return this.evalConfig
      .columns()
      .some((column) => this.isColumnNameInvalid(column));
  }

  hasUnsavedChanges(): boolean {
    return this.evalConfig.isDirty();
  }

  saveChanges(): Observable<void> {
    this.evalConfig.ensureRequiredNameColumn();
    const graderSnapshots = this.evalConfig
      .graders()
      .map((grader, index) =>
        grader.toSnapshot(index, this.evalConfig.evalConfigId),
      );
    const columnSnapshots = this.evalConfig
      .columns()
      .map((column, index) =>
        column.toSnapshot(index, this.evalConfig.evalConfigId),
      );
    const rowSnapshots = this.evalConfig
      .rows()
      .map((row, index) => row.toSnapshot(index, this.evalConfig.evalConfigId));
    const deletedGraderIds = this.evalConfig.getDeletedGraderIds();
    const deletedColumnIds = this.evalConfig.getDeletedColumnIds();
    const deletedRowIds = this.evalConfig.getDeletedRowIds();
    const validRowIds = new Set(
      this.evalConfig.rows().map((row) => row.evalRowId),
    );
    const validColumnIds = new Set(
      this.evalConfig.columns().map((column) => column.evalColumnId),
    );
    const dataSnapshots = this.evalConfig.dataStore
      .getDirtySnapshots()
      .filter(
        (entry) =>
          validRowIds.has(entry.evalRowId) &&
          validColumnIds.has(entry.evalColumnId),
      );

    return this.serverRepository.upsertEvalConfig(this.evalConfig).pipe(
      switchMap(() => this.serverRepository.upsertEvalGraders(graderSnapshots)),
      switchMap(() => {
        return this.serverRepository.upsertEvalColumns(columnSnapshots);
      }),
      switchMap(() => {
        return this.serverRepository.upsertEvalRows(rowSnapshots);
      }),
      switchMap(() => {
        if (
          deletedGraderIds.length === 0 &&
          deletedColumnIds.length === 0 &&
          deletedRowIds.length === 0
        ) {
          return of(undefined);
        }

        return forkJoin([
          ...deletedGraderIds.map((id) =>
            this.serverRepository.deleteEvalGrader(id),
          ),
          ...deletedColumnIds.map((id) =>
            this.serverRepository.deleteEvalColumn(id),
          ),
          ...deletedRowIds.map((id) => this.serverRepository.deleteEvalRow(id)),
        ]).pipe(map(() => undefined));
      }),
      switchMap(() => {
        if (dataSnapshots.length === 0) {
          return of(undefined);
        }

        return this.serverRepository.upsertEvalData(dataSnapshots);
      }),
      map(() => {
        this.evalConfig.markClean();
        this.ensureSelectedRow();
        return;
      }),
    );
  }

  runsPageCount(): number {
    return Math.ceil(this.runsTotal / this.runsPageSize);
  }

  runsPageNumbers(): number[] {
    return this.buildPageNumbers(this.runsPage, this.runsPageCount());
  }

  onRunsPageChange(page: number): void {
    const totalPages = this.runsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.runsPage) {
      return;
    }

    this.loadRunsPage(page);
  }

  onRunsSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    this.runsSearchText = input?.value ?? '';
    this.scheduleRunsSearch();
  }

  applyRunsSearch(): void {
    if (this.runSearchDebounceId) {
      clearTimeout(this.runSearchDebounceId);
      this.runSearchDebounceId = undefined;
    }

    this.runsPage = 1;
    this.refreshRuns();
  }

  refreshRuns(preferredRunId?: string): void {
    if (!this.hasLoadedConfig) {
      return;
    }

    this.isLoadingRuns = true;
    const search = this.runsSearchText.trim();
    this.serverRepository
      .getEvalRunCount(this.evalConfig.evalConfigId, search)
      .subscribe((total) => {
        this.runsTotal = total;
        const totalPages = this.runsPageCount();
        const nextPage =
          totalPages === 0 ? 1 : Math.min(this.runsPage, totalPages);
        this.loadRunsPage(nextPage, preferredRunId);
      });
  }

  selectRun(run: EvalRunSummarySnapshot): void {
    if (this.selectedRunId === run.evalRunId) {
      return;
    }

    this.selectedRunId = run.evalRunId;
    this.loadSelectedRunDetail();
  }

  openRunDetail(run: EvalRunSummarySnapshot): void {
    void this.router.navigate([
      '/evaluations',
      this.evalConfig.evalConfigId,
      'runs',
      run.evalRunId,
    ]);
  }

  canDeleteSelectedRun(): boolean {
    const selectedRun = this.selectedRunDetail?.evalRun;
    if (!selectedRun || this.isDeletingRun) {
      return false;
    }

    return selectedRun.status !== EvalRunStatus.Running;
  }

  deleteSelectedRun(): void {
    const selectedRun = this.selectedRunDetail?.evalRun;
    if (!selectedRun || this.isDeletingRun || selectedRun.status === EvalRunStatus.Running) {
      return;
    }

    const runName = selectedRun.name?.trim() || selectedRun.evalRunId;
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Evaluation Run',
        message: `Are you sure you want to delete the run '${runName}'?`,
      },
    });

    const modalRef = this.confirmModalRef;
    modalRef.onHidden?.pipe(take(1)).subscribe(() => {
      if (!modalRef.content?.result || this.isDeletingRun) {
        return;
      }

      this.isDeletingRun = true;
      this.serverRepository.deleteEvalRun(selectedRun.evalRunId).subscribe(() => {
        this.isDeletingRun = false;
        this.refreshRuns();
      });
    });
  }

  isRunSelected(run: EvalRunSummarySnapshot): boolean {
    return this.selectedRunId === run.evalRunId;
  }

  get sortedGraderSummaries(): EvalRunGraderSummaryDetailSnapshot[] {
    return (this.selectedRunDetail?.graderSummaries ?? []).slice();
  }

  getRunStatusLabel(status: EvalRunStatus): string {
    switch (status) {
      case EvalRunStatus.Running:
        return 'Running';
      case EvalRunStatus.Completed:
        return 'Completed';
      case EvalRunStatus.Failed:
        return 'Failed';
      case EvalRunStatus.Canceled:
        return 'Canceled';
      default:
        return 'Unknown';
    }
  }

  getRunStatusBadgeClass(status: EvalRunStatus): string {
    switch (status) {
      case EvalRunStatus.Running:
        return 'text-bg-primary';
      case EvalRunStatus.Completed:
        return 'text-bg-success';
      case EvalRunStatus.Failed:
        return 'text-bg-danger';
      case EvalRunStatus.Canceled:
        return 'text-bg-secondary';
      default:
        return 'text-bg-secondary';
    }
  }

  shouldShowRunListStatus(status: EvalRunStatus): boolean {
    return status === EvalRunStatus.Running || status === EvalRunStatus.Failed;
  }

  getRunListStatusLabel(status: EvalRunStatus): string {
    switch (status) {
      case EvalRunStatus.Running:
        return 'Running';
      case EvalRunStatus.Failed:
        return 'Failed';
      default:
        return '';
    }
  }

  formatDateTime(value: string | null): string {
    if (!value) {
      return '-';
    }

    return new Date(value).toLocaleString();
  }

  formatMetric(value: number | null): string {
    if (value === null || value === undefined) {
      return '-';
    }

    return value.toFixed(3);
  }

  formatPercent(value: number | null): string {
    if (value === null || value === undefined) {
      return '-';
    }

    return `${(value * 100).toFixed(1)}%`;
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  private loadEvalConfig(id: string): void {
    this.serverRepository.getEvalConfigDetail(id).subscribe((detail) => {
      if (!detail) {
        void this.router.navigate(['/evaluations']);
        return;
      }

      this.evalConfig = this.createEvalConfigFromDetail(detail);
      this.evalConfig.markClean();
      this.evalConfig.ensureRequiredNameColumn();
      this.ensureSelectedRow();
      this.hasLoadedConfig = true;
      this.tryNormalizeWorkflowSelection();
      if (this.activeTabId === 'runs') {
        this.ensureRunsLoaded();
      }
    });
  }

  private createEvalConfigFromDetail(
    detail: EvalConfigDetailSnapshot,
  ): EvalConfig {
    const snapshot = {
      ...detail.evalConfig,
      graders: detail.graders,
      columns: detail.columns,
      rows: detail.rows,
    };

    return new EvalConfig(snapshot, detail.data);
  }

  private loadWorkflows(): void {
    this.serverRepository
      .getWorkflowSummaries(
        '',
        0,
        0,
        WorkflowSortField.Name,
        SortDirection.Ascending,
      )
      .subscribe((workflows) => {
        this.workflowSummaries = workflows;
        this.hasLoadedWorkflows = true;
        this.tryNormalizeWorkflowSelection();
      });
  }

  private tryNormalizeWorkflowSelection(): void {
    if (
      this.normalizedWorkflowSelection ||
      !this.hasLoadedConfig ||
      !this.hasLoadedWorkflows
    ) {
      return;
    }

    this.normalizedWorkflowSelection = true;
    let changed = false;
    const workflowId = this.evalConfig.workflowId();
    if (workflowId) {
      const exists = this.workflowSummaries.some(
        (workflow) => workflow.id === workflowId,
      );
      if (!exists) {
        this.evalConfig.workflowId.set(null);
        changed = true;
      }
    }

    changed = this.normalizeGraderWorkflows() || changed;
    if (changed) {
      this.evalConfig.markClean();
    }
    this.evalConfig.ensureRequiredNameColumn();
    this.ensureSelectedRow();
  }

  private normalizeGraderWorkflows(): boolean {
    const graders = this.evalConfig.graders();
    if (!graders.length) {
      return false;
    }

    let changed = false;
    graders.forEach((grader) => {
      const workflowId = grader.workflowId();
      if (!workflowId) {
        return;
      }

      const exists = this.workflowSummaries.some(
        (workflow) => workflow.id === workflowId,
      );
      if (!exists) {
        grader.workflowId.set(null);
        changed = true;
      }
    });

    if (changed) {
      this.evalConfig.graders.set([...graders]);
    }

    return changed;
  }

  private ensureRunsLoaded(): void {
    if (!this.hasLoadedConfig) {
      return;
    }

    if (!this.runsLoaded) {
      this.refreshRuns();
    }
  }

  private loadRunsPage(page: number, preferredRunId?: string): void {
    this.isLoadingRuns = true;
    const skip = (page - 1) * this.runsPageSize;
    const search = this.runsSearchText.trim();
    this.serverRepository
      .getEvalRunSummaries(
        this.evalConfig.evalConfigId,
        search,
        skip,
        this.runsPageSize,
        EvalRunSortField.Started,
        SortDirection.Descending,
      )
      .subscribe((runs) => {
        this.runs = runs;
        this.runsPage = page;
        this.runsLoaded = true;
        this.isLoadingRuns = false;

        const selectedRunId = this.selectActiveRunAfterRefresh(preferredRunId);
        if (selectedRunId) {
          this.loadSelectedRunDetail();
        } else {
          this.selectedRunDetail = null;
        }
      });
  }

  private loadSelectedRunDetail(): void {
    if (!this.selectedRunId) {
      this.selectedRunDetail = null;
      return;
    }

    this.isLoadingRunDetail = true;
    this.serverRepository.getEvalRunDetail(this.selectedRunId).subscribe((detail) => {
      this.isLoadingRunDetail = false;
      this.selectedRunDetail = detail;
    });
  }

  private selectActiveRunAfterRefresh(preferredRunId?: string): string | null {
    if (this.runs.length === 0) {
      this.selectedRunId = null;
      return null;
    }

    if (preferredRunId && this.runs.some((run) => run.evalRunId === preferredRunId)) {
      this.selectedRunId = preferredRunId;
      return this.selectedRunId;
    }

    if (
      this.selectedRunId &&
      this.runs.some((run) => run.evalRunId === this.selectedRunId)
    ) {
      return this.selectedRunId;
    }

    this.selectedRunId = this.runs[0].evalRunId;
    return this.selectedRunId;
  }

  private scheduleRunsSearch(): void {
    if (this.runSearchDebounceId) {
      clearTimeout(this.runSearchDebounceId);
    }

    this.runSearchDebounceId = setTimeout(() => this.applyRunsSearch(), 250);
  }

  private buildPageNumbers(currentPage: number, totalPages: number): number[] {
    if (totalPages <= 1) {
      return [];
    }

    const windowSize = 5;
    let start = Math.max(1, currentPage - Math.floor(windowSize / 2));
    let end = Math.min(totalPages, start + windowSize - 1);
    start = Math.max(1, end - windowSize + 1);

    const pages: number[] = [];
    for (let page = start; page <= end; page += 1) {
      pages.push(page);
    }

    return pages;
  }

  private resolveTabId(tabId: string | null): string {
    if (tabId && this.tabIds.has(tabId)) {
      return tabId;
    }

    return this.defaultTabId;
  }

  private updateTabRoute(tabId: string): void {
    if (!this.tabIds.has(tabId)) {
      return;
    }

    const currentTabId = this.route.snapshot.queryParamMap.get('tab');
    if (currentTabId === tabId) {
      return;
    }

    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: tabId },
      queryParamsHandling: 'merge',
    });
  }

  private ensureSelectedRow(): void {
    const rows = this.evalConfig.rows();
    if (rows.length === 0) {
      this.selectedRowId = null;
      return;
    }

    if (
      this.selectedRowId &&
      rows.some((row) => row.evalRowId === this.selectedRowId)
    ) {
      return;
    }

    this.selectedRowId = rows[0].evalRowId;
  }

  private getRequiredNameColumn(): EvalColumn | null {
    const columns = this.evalConfig.columns();
    if (columns.length === 0) {
      return null;
    }

    return columns[0];
  }

  private findCellSnapshot(
    row: EvalRow,
    column: EvalColumn,
  ): EvalDataSnapshot | null {
    return this.evalConfig.dataStore.getSnapshot(
      row.evalRowId,
      column.evalColumnId,
    );
  }

  private getWritableCellSnapshot(
    row: EvalRow,
    column: EvalColumn,
  ): EvalDataSnapshot {
    const existing = this.evalConfig.dataStore.getSnapshot(
      row.evalRowId,
      column.evalColumnId,
    );
    if (existing) {
      return existing;
    }

    return this.createEmptyCellSnapshot(row.evalRowId, column.evalColumnId);
  }

  private createEmptyCellSnapshot(
    evalRowId: string,
    evalColumnId: string,
  ): EvalDataSnapshot {
    return {
      evalDataId: crypto.randomUUID(),
      evalRowId,
      evalColumnId,
      stringValue: null,
      intValue: null,
      doubleValue: null,
      boolValue: null,
    };
  }

  private seedMissingDataForColumn(column: EvalColumn): void {
    const rows = this.evalConfig.rows();
    rows.forEach((row) => {
      const existing = this.evalConfig.dataStore.getSnapshot(
        row.evalRowId,
        column.evalColumnId,
      );
      if (existing) {
        return;
      }

      const snapshot = this.createEmptyCellSnapshot(
        row.evalRowId,
        column.evalColumnId,
      );
      this.evalConfig.dataStore.upsert(snapshot, true);
    });
  }

  private seedMissingDataForRow(row: EvalRow): void {
    const columns = this.evalConfig.columns();
    columns.forEach((column) => {
      const existing = this.evalConfig.dataStore.getSnapshot(
        row.evalRowId,
        column.evalColumnId,
      );
      if (existing) {
        return;
      }

      const snapshot = this.createEmptyCellSnapshot(
        row.evalRowId,
        column.evalColumnId,
      );
      this.evalConfig.dataStore.upsert(snapshot, true);
    });
  }

  private setNumericCellValue(
    row: EvalRow,
    column: EvalColumn,
    value: string | number | null,
  ): void {
    const snapshot = this.getWritableCellSnapshot(row, column);
    const numeric =
      typeof value === 'number'
        ? value
        : value === null || value === ''
          ? NaN
          : Number(value);

    snapshot.stringValue = null;
    snapshot.boolValue = null;

    if (!Number.isFinite(numeric)) {
      snapshot.intValue = null;
      snapshot.doubleValue = null;
      this.evalConfig.dataStore.upsert(snapshot);
      return;
    }

    if (column.entryType() === ContextEntryType.Int) {
      snapshot.intValue = Math.trunc(numeric);
      snapshot.doubleValue = null;
    } else {
      snapshot.doubleValue = numeric;
      snapshot.intValue = null;
    }

    this.evalConfig.dataStore.upsert(snapshot);
  }

  private isAllowedColumnType(entryType: ContextEntryType): boolean {
    return (
      entryType !== ContextEntryType.AssetRef &&
      entryType !== ContextEntryType.AssetRefList
    );
  }
}
