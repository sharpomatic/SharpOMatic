import {
  AfterViewInit,
  Component,
  ElementRef,
  OnInit,
  ViewChild,
  inject,
} from '@angular/core';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { BsModalService, BsModalRef } from 'ngx-bootstrap/modal';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { WorkflowEntity } from '../../entities/definitions/workflow.entity';
import { WorkflowSummaryEntity } from '../../entities/definitions/workflow.summary.entity';
import { SamplesService } from '../../services/samples.service';
import { BsDropdownModule } from 'ngx-bootstrap/dropdown';

@Component({
  selector: 'app-workflows',
  standalone: true,
  imports: [CommonModule, RouterLink, BsDropdownModule],
  templateUrl: './workflows.component.html',
  styleUrls: ['./workflows.component.scss'],
  providers: [BsModalService],
})
export class WorkflowsComponent implements OnInit, AfterViewInit {
  private readonly serverWorkflow = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);
  private readonly samplesService = inject(SamplesService);
  private bsModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  public workflows: WorkflowSummaryEntity[] = [];
  public workflowsPage = 1;
  public workflowsTotal = 0;
  public readonly workflowsPageSize = 50;
  public isLoading = true;
  public searchText = '';
  public readonly sampleNames = this.samplesService.sampleNames;
  private searchDebounceId: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.refreshWorkflows();
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.searchInput?.nativeElement.focus(), 0);
  }

  newWorkflow(): void {
    const newWorkflow = WorkflowEntity.create(
      'Untitled',
      'New workflow needs a description.',
    );
    this.serverWorkflow.upsertWorkflow(newWorkflow).subscribe(() => {
      this.router.navigate(['/workflows', newWorkflow.id]);
    });
  }

  createWorkflowFromSample(sampleName: string): void {
    if (!sampleName) {
      return;
    }

    this.serverWorkflow
      .createWorkflowFromSample(sampleName)
      .subscribe((newWorkflowId) => {
        if (newWorkflowId) {
          this.router.navigate(['/workflows', newWorkflowId]);
        }
      });
  }

  deleteWorkflow(workflow: WorkflowSummaryEntity) {
    this.bsModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Workflow',
        message: `Are you sure you want to delete the workflow '${workflow.name()}'?`,
      },
    });

    this.bsModalRef.onHidden?.subscribe(() => {
      if (this.bsModalRef?.content?.result) {
        this.serverWorkflow.deleteWorkflow(workflow.id).subscribe(() => {
          this.refreshWorkflows();
        });
      }
    });
  }

  copyWorkflow(workflow: WorkflowSummaryEntity): void {
    this.serverWorkflow.copyWorkflow(workflow.id).subscribe((newWorkflowId) => {
      if (newWorkflowId) {
        this.refreshWorkflows();
      }
    });
  }

  workflowsPageCount(): number {
    return Math.ceil(this.workflowsTotal / this.workflowsPageSize);
  }

  workflowsPageNumbers(): number[] {
    const totalPages = this.workflowsPageCount();
    if (totalPages <= 1) {
      return [];
    }

    const currentPage = this.workflowsPage;
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

  onWorkflowsPageChange(page: number): void {
    const totalPages = this.workflowsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.workflowsPage) {
      return;
    }

    this.loadWorkflowsPage(page);
  }

  onSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    this.searchText = input?.value ?? '';
    this.scheduleSearch();
  }

  applySearch(): void {
    if (this.searchDebounceId) {
      clearTimeout(this.searchDebounceId);
      this.searchDebounceId = undefined;
    }

    this.workflowsPage = 1;
    this.refreshWorkflows();
  }

  private refreshWorkflows(): void {
    this.isLoading = true;
    const search = this.searchText.trim();
    this.serverWorkflow.getWorkflowCount(search).subscribe((total) => {
      this.workflowsTotal = total;
      const totalPages = this.workflowsPageCount();
      const nextPage =
        totalPages === 0 ? 1 : Math.min(this.workflowsPage, totalPages);
      this.loadWorkflowsPage(nextPage);
    });
  }

  private loadWorkflowsPage(page: number): void {
    this.isLoading = true;
    const skip = (page - 1) * this.workflowsPageSize;
    const search = this.searchText.trim();
    this.serverWorkflow
      .getWorkflowSummaries(search, skip, this.workflowsPageSize)
      .subscribe({
        next: (workflows) => {
          this.workflows = workflows;
          this.workflowsPage = page;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        },
      });
  }

  private scheduleSearch(): void {
    if (this.searchDebounceId) {
      clearTimeout(this.searchDebounceId);
    }

    this.searchDebounceId = setTimeout(() => this.applySearch(), 250);
  }
}
