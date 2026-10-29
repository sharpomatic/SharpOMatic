import {
  AfterViewInit,
  Component,
  ElementRef,
  ViewChild,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { finalize, forkJoin } from 'rxjs';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ModelSummary } from '../../metadata/definitions/model-summary';
import { Model } from '../../metadata/definitions/model';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { ModelSortField } from '../../enumerations/model-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';

@Component({
  selector: 'app-models',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './models.component.html',
  styleUrls: ['./models.component.scss'],
  providers: [BsModalService],
})
export class ModelsComponent implements AfterViewInit {
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  public models: ModelSummary[] = [];
  public modelsPage = 1;
  public modelsTotal = 0;
  public readonly modelsPageSize = 50;
  public isLoading = true;
  public searchText = '';
  public selectedModelIds = new Set<string>();
  public isDeletingSelected = false;
  public modelsSortField: ModelSortField = ModelSortField.Name;
  public modelsSortDirection: SortDirection = SortDirection.Ascending;
  public readonly ModelSortField = ModelSortField;
  public readonly SortDirection = SortDirection;
  private searchDebounceId: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.refreshModels();
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.searchInput?.nativeElement.focus(), 0);
  }

  newModel(): void {
    const newModel = new Model({
      ...Model.defaultSnapshot(),
      name: 'Untitled',
      description: 'Model needs a description.',
    });

    this.serverRepository.upsertModel(newModel).subscribe(() => {
      this.router.navigate(['/models', newModel.modelId]);
    });
  }

  selectedModelCount(): number {
    return this.selectedModelIds.size;
  }

  isModelSelected(modelId: string): boolean {
    return this.selectedModelIds.has(modelId);
  }

  onModelSelectionChange(modelId: string, event: Event): void {
    const input = event.target as HTMLInputElement | null;
    if (input?.checked) {
      this.selectedModelIds.add(modelId);
      return;
    }

    this.selectedModelIds.delete(modelId);
  }

  deleteSelectedModels(): void {
    if (this.selectedModelIds.size === 0 || this.isDeletingSelected) {
      return;
    }

    const selectedModels = this.models.filter((model) =>
      this.selectedModelIds.has(model.modelId),
    );
    if (selectedModels.length === 0) {
      this.clearSelectedModels();
      return;
    }

    const count = selectedModels.length;
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Models',
        message: `Are you sure you want to delete ${count} selected model${count === 1 ? '' : 's'}?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (!this.confirmModalRef?.content?.result) {
        return;
      }

      this.isDeletingSelected = true;
      forkJoin(
        selectedModels.map((model) =>
          this.serverRepository.deleteModel(model.modelId),
        ),
      )
        .pipe(
          finalize(() => {
            this.isDeletingSelected = false;
            this.clearSelectedModels();
            this.refreshModels();
          }),
        )
        .subscribe();
    });
  }

  modelsPageCount(): number {
    return Math.ceil(this.modelsTotal / this.modelsPageSize);
  }

  modelsPageNumbers(): number[] {
    const totalPages = this.modelsPageCount();
    if (totalPages <= 1) {
      return [];
    }

    const currentPage = this.modelsPage;
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

  onModelsPageChange(page: number): void {
    const totalPages = this.modelsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.modelsPage) {
      return;
    }

    this.clearSelectedModels();
    this.loadModelsPage(page);
  }

  onModelsSortChange(field: ModelSortField): void {
    if (this.modelsSortField === field) {
      this.modelsSortDirection =
        this.modelsSortDirection === SortDirection.Descending
          ? SortDirection.Ascending
          : SortDirection.Descending;
    } else {
      this.modelsSortField = field;
      this.modelsSortDirection = SortDirection.Ascending;
    }

    this.clearSelectedModels();
    this.modelsPage = 1;
    this.refreshModels();
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

    this.clearSelectedModels();
    this.modelsPage = 1;
    this.refreshModels();
  }

  private refreshModels(): void {
    this.isLoading = true;
    const search = this.searchText.trim();
    this.serverRepository.getModelCount(search).subscribe((total) => {
      this.modelsTotal = total;
      const totalPages = this.modelsPageCount();
      const nextPage =
        totalPages === 0 ? 1 : Math.min(this.modelsPage, totalPages);
      this.loadModelsPage(nextPage);
    });
  }

  private loadModelsPage(page: number): void {
    this.isLoading = true;
    const skip = (page - 1) * this.modelsPageSize;
    const search = this.searchText.trim();
    this.serverRepository
      .getModelSummaries(
        search,
        skip,
        this.modelsPageSize,
        this.modelsSortField,
        this.modelsSortDirection,
      )
      .subscribe({
        next: (models) => {
          this.models = models;
          this.syncSelectedModelsWithPage();
          this.modelsPage = page;
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

  private clearSelectedModels(): void {
    this.selectedModelIds.clear();
  }

  private syncSelectedModelsWithPage(): void {
    if (this.selectedModelIds.size === 0) {
      return;
    }

    const visibleIds = new Set(this.models.map((model) => model.modelId));
    for (const selectedId of Array.from(this.selectedModelIds)) {
      if (!visibleIds.has(selectedId)) {
        this.selectedModelIds.delete(selectedId);
      }
    }
  }
}
