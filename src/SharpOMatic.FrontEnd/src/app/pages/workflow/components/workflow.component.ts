import { Component, HostListener, TemplateRef, ViewChild, inject, OnInit, signal, computed, effect } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { WorkflowService } from '../services/workflow.service';
import { DesignerComponent } from '../../../components/designer/components/designer.component';
import { DesignerUpdateService } from '../../../components/designer/services/designer-update.service';
import { BsDropdownModule } from 'ngx-bootstrap/dropdown';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BsModalService } from 'ngx-bootstrap/modal';
import { TracebarComponent } from './tracebar.component';
import { NodeType } from '../../../entities/enumerations/node-type';
import { TabComponent, TabItem } from '../../../components/tab/tab.component';
import { CanLeaveWithUnsavedChanges } from '../../../guards/unsaved-changes.guard';
import { RunStatus } from '../../../enumerations/run-status';
import { RunSortField } from '../../../enumerations/run-sort-field';
import { SortDirection } from '../../../enumerations/sort-direction';
import { RunProgressModel } from '../interfaces/run-progress-model';
import { Observable } from 'rxjs';
import { DialogService } from '../../../dialogs/services/dialog.service';
import { RunViewerDialogComponent } from '../../../dialogs/run-viewer/run-viewer-dialog.component';

@Component({
  selector: 'app-workflow',
  imports: [
    CommonModule,
    FormsModule,
    BsDropdownModule,
    TabComponent,
    DesignerComponent,
    TracebarComponent
  ],
  templateUrl: './workflow.component.html',
  styleUrl: './workflow.component.scss'
})
export class WorkflowComponent implements OnInit, CanLeaveWithUnsavedChanges {
  @ViewChild('designTab', { static: true }) designTab!: TemplateRef<unknown>;
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('runsTab', { static: true }) runsTab!: TemplateRef<unknown>;

  public readonly route = inject(ActivatedRoute);
  public readonly router = inject(Router);
  private readonly dialogService = inject(DialogService);
  public readonly updateService = inject(DesignerUpdateService);
  public readonly workflowService = inject(WorkflowService);
  public readonly modalService = inject(BsModalService);

  private readonly tabIds = new Set(['details', 'design', 'runs']);
  private readonly defaultTabId = 'design';

  public activeTabId = this.defaultTabId;
  public tracebarActiveTabId = 'input';
  public isTracebarClosed = signal(true);
  public tracebarWidth = signal(800);
  public tabs: TabItem[] = [];
  public readonly hasStartNode = computed(() => this.workflowService.workflow().nodes().some(node => node.nodeType() === NodeType.Start));
  public readonly RunStatus = RunStatus;
  public readonly RunSortField = RunSortField;
  public readonly SortDirection = SortDirection;
  public readonly runsPageCount = computed(() => this.workflowService.getRunsPageCount());
  public readonly runsPageNumbers = computed(() => {
    const totalPages = this.runsPageCount();
    if (totalPages <= 1) {
      return [];
    }

    const currentPage = this.workflowService.runsPage();
    const windowSize = 5;
    let start = Math.max(1, currentPage - Math.floor(windowSize / 2));
    let end = Math.min(totalPages, start + windowSize - 1);
    start = Math.max(1, end - windowSize + 1);

    const pages: number[] = [];
    for (let page = start; page <= end; page += 1) {
      pages.push(page);
    }

    return pages;
  });

  ngOnInit(): void {
    this.tabs = [
      { id: 'details', title: 'Details', content: this.detailsTab },
      { id: 'design', title: 'Design', content: this.designTab },
      { id: 'runs', title: 'Runs', content: this.runsTab }
    ];

    this.activeTabId = this.resolveTabId(this.route.snapshot.queryParamMap.get('tab'));
    this.route.queryParamMap.subscribe(params => {
      const nextTabId = this.resolveTabId(params.get('tab'));
      if (nextTabId !== this.activeTabId) {
        this.activeTabId = nextTabId;
        this.reloadRunsForTab(nextTabId);
      }
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.workflowService.load(id);
    }
  }

  save(): void {
    this.saveChanges().subscribe();
  }

  run(): void {
    const hasRunInputs = this.workflowService.runInputs().entries().length > 0;
    if (!hasRunInputs) {
      this.tracebarActiveTabId = 'trace';
    }

    this.workflowService.run().subscribe(() => {
      this.isTracebarClosed.set(false);
      if (!hasRunInputs) {
        this.tracebarActiveTabId = 'trace';
      }
    });
  }

  toggleTracebar(): void {
    this.isTracebarClosed.set(!this.isTracebarClosed());
  }

  onActiveTabChange(tab: TabItem): void {
    this.updateTabRoute(tab.id);
    this.reloadRunsForTab(tab.id);
  }

  onRunRowDoubleClick(run: RunProgressModel): void {
    this.dialogService.open(RunViewerDialogComponent, { run });
  }

  onRunsPageChange(page: number): void {
    const totalPages = this.runsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.workflowService.runsPage()) {
      return;
    }

    this.workflowService.loadRunsPage(page);
  }

  onRunsSortChange(field: RunSortField): void {
    this.workflowService.updateRunsSort(field);
  }

  onAddStartNode(event: Event): void {
    if (this.hasStartNode()) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    this.updateService.addStartNode(this.workflowService.workflow());
  }

  onTracebarWidthChange(width: number): void {
    this.tracebarWidth.set(width);
  }

  hasUnsavedChanges(): boolean {
    return this.workflowService.workflow().isDirty() || this.workflowService.runInputs().isDirty();
  }

  saveChanges(): Observable<void> {
    return this.workflowService.save();
  }

  public getRunDuration(run: RunProgressModel): string {
    if (!run.started || !run.stopped) {
      return '';
    }

    const startedMs = Date.parse(run.started);
    const stoppedMs = Date.parse(run.stopped);
    if (!Number.isFinite(startedMs) || !Number.isFinite(stoppedMs) || stoppedMs < startedMs) {
      return '';
    }

    const durationMs = stoppedMs - startedMs;
    if (durationMs <= 0) {
      return '';
    }

    const roundedMs = Math.ceil(durationMs / 10) * 10;
    const totalSeconds = Math.floor(roundedMs / 1000);
    const hundredths = Math.floor((roundedMs % 1000) / 10);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}.${hundredths.toString().padStart(2, '0')}`;
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
      queryParamsHandling: 'merge'
    });
  }

  private reloadRunsForTab(tabId: string): void {
    if (tabId !== 'runs') {
      return;
    }

    this.workflowService.loadRunsPage(this.workflowService.runsPage());
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
}
