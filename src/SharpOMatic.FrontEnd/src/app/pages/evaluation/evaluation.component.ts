import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, TemplateRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, forkJoin, map, of, switchMap } from 'rxjs';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { EvalConfig } from '../../eval/definitions/eval-config';
import { EvalConfigDetailSnapshot } from '../../eval/definitions/eval-config-detail';
import { EvalGrader } from '../../eval/definitions/eval-grader';
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

  get workflowSelectionId(): string | null {
    const workflowId = this.evalConfig.workflowId();
    if (!workflowId) {
      return null;
    }

    const exists = this.workflowSummaries.some(workflow => workflow.id === workflowId);
    return exists ? workflowId : null;
  }

  graderWorkflowSelectionId(grader: EvalGrader): string | null {
    const workflowId = grader.workflowId();
    if (!workflowId) {
      return null;
    }

    const exists = this.workflowSummaries.some(workflow => workflow.id === workflowId);
    return exists ? workflowId : null;
  }

  appendGrader(): void {
    const graders = this.evalConfig.graders();
    const snapshot = EvalGrader.defaultSnapshot(graders.length, this.evalConfig.evalConfigId);
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

  hasUnsavedChanges(): boolean {
    return this.evalConfig.isDirty();
  }

  saveChanges(): Observable<void> {
    const graderSnapshots = this.evalConfig.graders().map((grader, index) =>
      grader.toSnapshot(index, this.evalConfig.evalConfigId)
    );
    const deletedGraderIds = this.evalConfig.getDeletedGraderIds();

    return this.serverRepository.upsertEvalConfig(this.evalConfig).pipe(
      switchMap(() => this.serverRepository.upsertEvalGraders(this.evalConfig.evalConfigId, graderSnapshots)),
      switchMap(() => {
        if (deletedGraderIds.length === 0) {
          return of(undefined);
        }

        return forkJoin(deletedGraderIds.map(id => this.serverRepository.deleteEvalGrader(id))).pipe(
          map(() => undefined)
        );
      }),
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
    this.serverRepository.getEvalConfigDetail(id).subscribe(detail => {
      if (!detail) {
        void this.router.navigate(['/evaluations']);
        return;
      }

      this.evalConfig = this.createEvalConfigFromDetail(detail);
      this.evalConfig.markClean();
      this.hasLoadedConfig = true;
      this.tryNormalizeWorkflowSelection();
    });
  }

  private createEvalConfigFromDetail(detail: EvalConfigDetailSnapshot): EvalConfig {
    const snapshot = {
      ...detail.evalConfig,
      graders: detail.graders,
      columns: detail.columns,
      rows: detail.rows,
    };

    return new EvalConfig(snapshot, detail.data);
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
    if (workflowId) {
      const exists = this.workflowSummaries.some(workflow => workflow.id === workflowId);
      if (!exists) {
        this.evalConfig.workflowId.set(null);
      }
    }

    this.normalizeGraderWorkflows();
    this.evalConfig.markClean();
  }

  private normalizeGraderWorkflows(): void {
    const graders = this.evalConfig.graders();
    if (!graders.length) {
      return;
    }

    let changed = false;
    graders.forEach(grader => {
      const workflowId = grader.workflowId();
      if (!workflowId) {
        return;
      }

      const exists = this.workflowSummaries.some(workflow => workflow.id === workflowId);
      if (!exists) {
        grader.workflowId.set(null);
        changed = true;
      }
    });

    if (changed) {
      this.evalConfig.graders.set([...graders]);
    }
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
