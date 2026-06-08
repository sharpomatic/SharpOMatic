import {
  AfterViewInit,
  Component,
  ElementRef,
  OnInit,
  ViewChild,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { BsDropdownModule } from 'ngx-bootstrap/dropdown';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { finalize, forkJoin } from 'rxjs';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { InformationDialogComponent } from '../../dialogs/information/information-dialog.component';
import {
  MoveAssetsDialogComponent,
  MoveAssetsTopLevelOptionValue,
} from '../../dialogs/move-assets/move-assets-dialog.component';
import { TextInputDialogComponent } from '../../dialogs/text-input/text-input-dialog.component';
import { WorkflowEntity } from '../../entities/definitions/workflow.entity';
import { WorkflowSummaryEntity } from '../../entities/definitions/workflow.summary.entity';
import { SortDirection } from '../../enumerations/sort-direction';
import { WorkflowSortField } from '../../enumerations/workflow-sort-field';
import { SamplesService } from '../../services/samples.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { WorkflowFolderSummary } from './interfaces/workflow-folder-summary';

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
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  private infoModalRef: BsModalRef<InformationDialogComponent> | undefined;
  private folderNameModalRef: BsModalRef<TextInputDialogComponent> | undefined;
  private moveWorkflowsModalRef:
    | BsModalRef<MoveAssetsDialogComponent>
    | undefined;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  public workflows: WorkflowSummaryEntity[] = [];
  public folders: WorkflowFolderSummary[] = [];
  public selectedFolderFilter = 'all';
  public workflowsPage = 1;
  public workflowsTotal = 0;
  public readonly workflowsPageSize = 50;
  public isLoading = true;
  public searchText = '';
  public selectedWorkflowIds = new Set<string>();
  public isDeletingSelected = false;
  public isMovingSelected = false;
  public workflowsSortField: WorkflowSortField = WorkflowSortField.Name;
  public workflowsSortDirection: SortDirection = SortDirection.Ascending;
  public readonly WorkflowSortField = WorkflowSortField;
  public readonly SortDirection = SortDirection;
  public readonly sampleNames = this.samplesService.sampleNames;
  private searchDebounceId: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.refreshFolders();
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
    newWorkflow.workflowFolderId.set(this.currentFolderId());
    this.serverWorkflow.upsertWorkflow(newWorkflow).subscribe(() => {
      this.router.navigate(['/workflows', newWorkflow.id]);
    });
  }

  createWorkflowFromSample(sampleName: string): void {
    if (!sampleName) {
      return;
    }

    this.serverWorkflow
      .createWorkflowFromSample(sampleName, this.currentFolderId())
      .subscribe((newWorkflowId) => {
        if (newWorkflowId) {
          this.router.navigate(['/workflows', newWorkflowId]);
        }
      });
  }

  createFolder(): void {
    this.folderNameModalRef = this.modalService.show(TextInputDialogComponent, {
      initialState: {
        title: 'Add Folder',
        label: 'Folder name',
        value: '',
        placeholder: 'Enter folder name',
        confirmText: 'Add',
      },
    });

    this.folderNameModalRef.onHidden?.subscribe(() => {
      const name = this.folderNameModalRef?.content?.result;
      if (!name) {
        return;
      }

      this.serverWorkflow.createWorkflowFolder(name).subscribe((folder) => {
        if (!folder) {
          return;
        }

        this.selectedFolderFilter = folder.workflowFolderId;
        this.refreshFolders();
        this.refreshWorkflows();
      });
    });
  }

  editSelectedFolder(): void {
    const folderId = this.currentFolderId();
    if (!folderId) {
      return;
    }

    const folder = this.folders.find(
      (entry) => entry.workflowFolderId === folderId,
    );
    if (!folder) {
      return;
    }

    this.folderNameModalRef = this.modalService.show(TextInputDialogComponent, {
      initialState: {
        title: 'Rename Folder',
        label: 'Folder name',
        value: folder.name,
        placeholder: 'Enter folder name',
        confirmText: 'Rename',
      },
    });

    this.folderNameModalRef.onHidden?.subscribe(() => {
      const name = this.folderNameModalRef?.content?.result;
      if (!name) {
        return;
      }

      this.serverWorkflow
        .renameWorkflowFolder(folderId, name)
        .subscribe((updated) => {
          if (!updated) {
            return;
          }

          this.refreshFolders();
          this.refreshWorkflows();
        });
    });
  }

  deleteSelectedFolder(): void {
    const folderId = this.currentFolderId();
    if (!folderId) {
      return;
    }

    const folder = this.folders.find(
      (entry) => entry.workflowFolderId === folderId,
    );
    if (!folder) {
      return;
    }

    this.serverWorkflow
      .getWorkflowCount('', folderId, false)
      .subscribe((count) => {
        if (count > 0) {
          this.showInfoDialog(
            'Delete Folder',
            `Folder '${folder.name}' contains ${count} workflow${count === 1 ? '' : 's'} and cannot be deleted.`,
          );
          return;
        }

        this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
          initialState: {
            title: 'Delete Folder',
            message: `Delete folder '${folder.name}'?`,
          },
        });

        this.confirmModalRef.onHidden?.subscribe(() => {
          if (!this.confirmModalRef?.content?.result) {
            return;
          }

          this.serverWorkflow
            .deleteWorkflowFolder(folderId)
            .subscribe((success) => {
              if (!success) {
                return;
              }

              this.selectedFolderFilter = 'all';
              this.refreshFolders();
              this.refreshWorkflows();
            });
        });
      });
  }

  selectAllWorkflows(): void {
    if (this.selectedFolderFilter === 'all') {
      return;
    }

    this.clearSelectedWorkflows();
    this.selectedFolderFilter = 'all';
    this.workflowsPage = 1;
    this.refreshWorkflows();
  }

  selectTopLevelWorkflows(): void {
    if (this.selectedFolderFilter === 'top') {
      return;
    }

    this.clearSelectedWorkflows();
    this.selectedFolderFilter = 'top';
    this.workflowsPage = 1;
    this.refreshWorkflows();
  }

  selectFolderWorkflows(folderId: string): void {
    if (this.selectedFolderFilter === folderId) {
      return;
    }

    this.clearSelectedWorkflows();
    this.selectedFolderFilter = folderId;
    this.workflowsPage = 1;
    this.refreshWorkflows();
  }

  isFolderFilterSelected(folderId: string): boolean {
    return this.selectedFolderFilter === folderId;
  }

  copyWorkflow(workflow: WorkflowSummaryEntity): void {
    this.serverWorkflow.copyWorkflow(workflow.id).subscribe((newWorkflowId) => {
      if (newWorkflowId) {
        this.refreshFolders();
        this.refreshWorkflows();
      }
    });
  }

  deleteWorkflow(workflow: WorkflowSummaryEntity): void {
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Workflow',
        message: `Are you sure you want to delete the workflow '${workflow.name()}'?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (!this.confirmModalRef?.content?.result) {
        return;
      }

      this.serverWorkflow.deleteWorkflow(workflow.id).subscribe(() => {
        this.refreshFolders();
        this.refreshWorkflows();
      });
    });
  }

  selectedWorkflowCount(): number {
    return this.selectedWorkflowIds.size;
  }

  isWorkflowSelected(workflowId: string): boolean {
    return this.selectedWorkflowIds.has(workflowId);
  }

  onWorkflowSelectionChange(workflowId: string, event: Event): void {
    const input = event.target as HTMLInputElement | null;
    if (input?.checked) {
      this.selectedWorkflowIds.add(workflowId);
      return;
    }

    this.selectedWorkflowIds.delete(workflowId);
  }

  deleteSelectedWorkflows(): void {
    if (
      this.selectedWorkflowIds.size === 0 ||
      this.isDeletingSelected ||
      this.isMovingSelected
    ) {
      return;
    }

    const selectedWorkflows = this.workflows.filter((workflow) =>
      this.selectedWorkflowIds.has(workflow.id),
    );
    if (selectedWorkflows.length === 0) {
      this.clearSelectedWorkflows();
      return;
    }

    const count = selectedWorkflows.length;
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Workflows',
        message: `Are you sure you want to delete ${count} selected workflow${count === 1 ? '' : 's'}?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (!this.confirmModalRef?.content?.result) {
        return;
      }

      this.isDeletingSelected = true;
      forkJoin(
        selectedWorkflows.map((workflow) =>
          this.serverWorkflow.deleteWorkflow(workflow.id),
        ),
      )
        .pipe(
          finalize(() => {
            this.isDeletingSelected = false;
            this.clearSelectedWorkflows();
            this.refreshFolders();
            this.refreshWorkflows();
          }),
        )
        .subscribe();
    });
  }

  moveSelectedWorkflows(): void {
    if (
      this.selectedWorkflowIds.size === 0 ||
      this.isDeletingSelected ||
      this.isMovingSelected
    ) {
      return;
    }

    const selectedWorkflows = this.workflows.filter((workflow) =>
      this.selectedWorkflowIds.has(workflow.id),
    );
    if (selectedWorkflows.length === 0) {
      this.clearSelectedWorkflows();
      return;
    }

    this.moveWorkflowsModalRef = this.modalService.show(
      MoveAssetsDialogComponent,
      {
        initialState: {
          title: 'Move Workflows',
          folders: this.folders.map((folder) => ({
            folderId: folder.workflowFolderId,
            name: folder.name,
          })),
          selectedFolderId: null,
        },
      },
    );

    this.moveWorkflowsModalRef.onHidden?.subscribe(() => {
      const targetFolderValue = this.moveWorkflowsModalRef?.content?.result;
      if (targetFolderValue === undefined) {
        return;
      }

      const targetFolderId =
        targetFolderValue === MoveAssetsTopLevelOptionValue
          ? null
          : targetFolderValue;

      this.isMovingSelected = true;
      forkJoin(
        selectedWorkflows.map((workflow) =>
          this.serverWorkflow.moveWorkflowToFolder(workflow.id, targetFolderId),
        ),
      )
        .pipe(
          finalize(() => {
            this.isMovingSelected = false;
            this.clearSelectedWorkflows();
            this.refreshFolders();
            this.refreshWorkflows();
          }),
        )
        .subscribe();
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

    this.clearSelectedWorkflows();
    this.loadWorkflowsPage(page);
  }

  onWorkflowsSortChange(field: WorkflowSortField): void {
    if (this.workflowsSortField === field) {
      this.workflowsSortDirection =
        this.workflowsSortDirection === SortDirection.Descending
          ? SortDirection.Ascending
          : SortDirection.Descending;
    } else {
      this.workflowsSortField = field;
      this.workflowsSortDirection = SortDirection.Ascending;
    }

    this.clearSelectedWorkflows();
    this.workflowsPage = 1;
    this.refreshWorkflows();
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

    this.clearSelectedWorkflows();
    this.workflowsPage = 1;
    this.refreshWorkflows();
  }

  private refreshWorkflows(): void {
    this.isLoading = true;
    const search = this.searchText.trim();
    const folderId = this.currentFolderId();
    const topLevelOnly = this.selectedFolderFilter === 'top';
    this.serverWorkflow
      .getWorkflowCount(search, folderId ?? undefined, topLevelOnly)
      .subscribe((total) => {
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
    const folderId = this.currentFolderId();
    const topLevelOnly = this.selectedFolderFilter === 'top';
    this.serverWorkflow
      .getWorkflowSummaries(
        search,
        skip,
        this.workflowsPageSize,
        this.workflowsSortField,
        this.workflowsSortDirection,
        folderId ?? undefined,
        topLevelOnly,
      )
      .subscribe({
        next: (workflows) => {
          this.workflows = workflows;
          this.syncSelectedWorkflowsWithPage();
          this.workflowsPage = page;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
        },
      });
  }

  private refreshFolders(): void {
    this.serverWorkflow
      .getWorkflowFolders('', 0, 0, SortDirection.Ascending)
      .subscribe((folders) => {
        this.folders = folders;
        if (
          this.selectedFolderFilter !== 'all' &&
          this.selectedFolderFilter !== 'top' &&
          !this.folders.some(
            (folder) => folder.workflowFolderId === this.selectedFolderFilter,
          )
        ) {
          this.selectedFolderFilter = 'all';
        }
      });
  }

  private scheduleSearch(): void {
    if (this.searchDebounceId) {
      clearTimeout(this.searchDebounceId);
    }

    this.searchDebounceId = setTimeout(() => this.applySearch(), 250);
  }

  private showInfoDialog(title: string, message: string): void {
    this.infoModalRef = this.modalService.show(InformationDialogComponent, {
      initialState: {
        title,
        message,
      },
    });
  }

  private clearSelectedWorkflows(): void {
    this.selectedWorkflowIds.clear();
  }

  private syncSelectedWorkflowsWithPage(): void {
    if (this.selectedWorkflowIds.size === 0) {
      return;
    }

    const visibleIds = new Set(this.workflows.map((workflow) => workflow.id));
    for (const selectedId of Array.from(this.selectedWorkflowIds)) {
      if (!visibleIds.has(selectedId)) {
        this.selectedWorkflowIds.delete(selectedId);
      }
    }
  }

  private currentFolderId(): string | null {
    if (
      this.selectedFolderFilter === 'all' ||
      this.selectedFolderFilter === 'top'
    ) {
      return null;
    }

    return this.selectedFolderFilter;
  }
}
