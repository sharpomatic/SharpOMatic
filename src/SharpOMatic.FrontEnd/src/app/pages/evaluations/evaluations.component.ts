import { AfterViewInit, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { EvalConfig } from '../../eval/definitions/eval-config';
import { EvalConfigSummary } from '../../eval/definitions/eval-config-summary';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';

@Component({
  selector: 'app-evaluations',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
  ],
  templateUrl: './evaluations.component.html',
  styleUrls: ['./evaluations.component.scss'],
  providers: [BsModalService],
})
export class EvaluationsComponent implements AfterViewInit {
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  public evalConfigs: EvalConfigSummary[] = [];
  public evalConfigsPage = 1;
  public evalConfigsTotal = 0;
  public readonly evalConfigsPageSize = 50;
  public isLoading = true;
  public searchText = '';
  private searchDebounceId: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.refreshEvalConfigs();
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.searchInput?.nativeElement.focus(), 0);
  }

  newEvalConfig(): void {
    const newConfig = EvalConfig.fromSnapshot({
      ...EvalConfig.defaultSnapshot(),
      name: 'Untitled',
      description: 'Evaluation needs a description.',
    });

    this.serverRepository.upsertEvalConfig(newConfig).subscribe(() => {
      this.router.navigate(['/evaluations', newConfig.evalConfigId]);
    });
  }

  deleteEvalConfig(config: EvalConfigSummary): void {
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Evaluation',
        message: `Are you sure you want to delete the evaluation '${config.name}'?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (this.confirmModalRef?.content?.result) {
        this.serverRepository.deleteEvalConfig(config.evalConfigId).subscribe(() => {
          this.refreshEvalConfigs();
        });
      }
    });
  }

  evalConfigsPageCount(): number {
    return Math.ceil(this.evalConfigsTotal / this.evalConfigsPageSize);
  }

  evalConfigsPageNumbers(): number[] {
    const totalPages = this.evalConfigsPageCount();
    if (totalPages <= 1) {
      return [];
    }

    const currentPage = this.evalConfigsPage;
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

  onEvalConfigsPageChange(page: number): void {
    const totalPages = this.evalConfigsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.evalConfigsPage) {
      return;
    }

    this.loadEvalConfigsPage(page);
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

    this.evalConfigsPage = 1;
    this.refreshEvalConfigs();
  }

  private refreshEvalConfigs(): void {
    this.isLoading = true;
    const search = this.searchText.trim();
    this.serverRepository.getEvalConfigCount(search).subscribe(total => {
      this.evalConfigsTotal = total;
      const totalPages = this.evalConfigsPageCount();
      const nextPage = totalPages === 0 ? 1 : Math.min(this.evalConfigsPage, totalPages);
      this.loadEvalConfigsPage(nextPage);
    });
  }

  private loadEvalConfigsPage(page: number): void {
    this.isLoading = true;
    const skip = (page - 1) * this.evalConfigsPageSize;
    const search = this.searchText.trim();
    this.serverRepository.getEvalConfigSummaries(search, skip, this.evalConfigsPageSize).subscribe({
      next: (configs) => {
        this.evalConfigs = configs;
        this.evalConfigsPage = page;
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
