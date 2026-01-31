import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, TemplateRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, map } from 'rxjs';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { EvalConfig } from '../../eval/definitions/eval-config';
import { CanLeaveWithUnsavedChanges } from '../../helper/unsaved-changes.guard';
import { WorkflowSummaryEntity } from '../../entities/definitions/workflow.summary.entity';
import { WorkflowSortField } from '../../enumerations/workflow-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';
import { ServerRepositoryService } from '../../services/server.repository.service';

@Component({
  selector: 'app-evaluation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TabComponent,
  ],
  templateUrl: './evaluation.component.html',
  styleUrls: ['./evaluation.component.scss'],
})
export class EvaluationComponent implements OnInit, CanLeaveWithUnsavedChanges {
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('gradersTab', { static: true }) gradersTab!: TemplateRef<unknown>;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly serverRepository = inject(ServerRepositoryService);

  public evalConfig = EvalConfig.fromSnapshot(EvalConfig.defaultSnapshot());
  private readonly tabIds = new Set(['details', 'graders']);
  private readonly defaultTabId = 'details';
  private hasLoadedConfig = false;
  private hasLoadedWorkflows = false;
  private normalizedWorkflowSelection = false;
  public workflowSummaries: WorkflowSummaryEntity[] = [];
  public tabs: TabItem[] = [];
  public activeTabId = this.defaultTabId;

  ngOnInit(): void {
    this.tabs = [
      { id: 'details', title: 'Details', content: this.detailsTab },
      { id: 'graders', title: 'Graders', content: this.gradersTab },
    ];

    this.activeTabId = this.resolveTabId(this.route.snapshot.queryParamMap.get('tab'));
    this.route.queryParamMap.subscribe(params => {
      const nextTabId = this.resolveTabId(params.get('tab'));
      if (nextTabId !== this.activeTabId) {
        this.activeTabId = nextTabId;
      }
    });

    this.loadWorkflows();

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadEvalConfig(id);
    } else {
      this.hasLoadedConfig = true;
    }
  }

  save(): void {
    this.saveChanges().subscribe();
  }

  onActiveTabChange(tab: TabItem): void {
    this.updateTabRoute(tab.id);
  }

  onWorkflowChange(value: string | null): void {
    this.evalConfig.workflowId.set(value ?? null);
  }

  onMaxParallelChange(value: string | number): void {
    const numeric = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(numeric)) {
      this.evalConfig.maxParallel.set(1);
      return;
    }

    this.evalConfig.maxParallel.set(Math.max(1, Math.trunc(numeric)));
  }

  get workflowSelectionId(): string | null {
    const workflowId = this.evalConfig.workflowId();
    if (!workflowId) {
      return null;
    }

    const exists = this.workflowSummaries.some(workflow => workflow.id === workflowId);
    return exists ? workflowId : null;
  }

  hasUnsavedChanges(): boolean {
    return this.evalConfig.isDirty();
  }

  saveChanges(): Observable<void> {
    return this.serverRepository.upsertEvalConfig(this.evalConfig).pipe(
      map(() => {
        this.evalConfig.markClean();
        return;
      })
    );
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  private loadEvalConfig(id: string): void {
    this.serverRepository.getEvalConfig(id).subscribe(config => {
      if (!config) {
        void this.router.navigate(['/evaluations']);
        return;
      }

      this.evalConfig = config;
      this.evalConfig.markClean();
      this.hasLoadedConfig = true;
      this.tryNormalizeWorkflowSelection();
    });
  }

  private loadWorkflows(): void {
    this.serverRepository.getWorkflowSummaries(
      '',
      0,
      0,
      WorkflowSortField.Name,
      SortDirection.Ascending
    ).subscribe(workflows => {
      this.workflowSummaries = workflows;
      this.hasLoadedWorkflows = true;
      this.tryNormalizeWorkflowSelection();
    });
  }

  private tryNormalizeWorkflowSelection(): void {
    if (this.normalizedWorkflowSelection || !this.hasLoadedConfig || !this.hasLoadedWorkflows) {
      return;
    }

    this.normalizedWorkflowSelection = true;
    const workflowId = this.evalConfig.workflowId();
    if (!workflowId) {
      this.evalConfig.markClean();
      return;
    }

    const exists = this.workflowSummaries.some(workflow => workflow.id === workflowId);
    if (!exists) {
      this.evalConfig.workflowId.set(null);
    }

    this.evalConfig.markClean();
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

}
