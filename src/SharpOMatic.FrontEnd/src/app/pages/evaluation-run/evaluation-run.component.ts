import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EvalRunDetailSnapshot } from '../../eval/definitions/eval-run-detail';
import {
  EvalRunRowDetailSnapshot,
  EvalRunRowGraderDetailSnapshot,
} from '../../eval/definitions/eval-run-row-detail';
import { EvalRunRowSortField } from '../../eval/enumerations/eval-run-row-sort-field';
import { EvalRunStatus } from '../../eval/enumerations/eval-run-status';
import { SortDirection } from '../../enumerations/sort-direction';
import { ServerRepositoryService } from '../../services/server.repository.service';

@Component({
  selector: 'app-evaluation-run',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './evaluation-run.component.html',
  styleUrls: ['./evaluation-run.component.scss'],
})
export class EvaluationRunComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly serverRepository = inject(ServerRepositoryService);

  private readonly runRowsPageSize = 25;
  private runRowsSearchDebounceId: ReturnType<typeof setTimeout> | undefined;

  public readonly evalRunStatus = EvalRunStatus;
  public evalConfigId = '';
  public evalRunId = '';
  public runDetail: EvalRunDetailSnapshot | null = null;
  public isLoadingRunDetail = false;
  public runRows: EvalRunRowDetailSnapshot[] = [];
  public runRowsTotal = 0;
  public runRowsPage = 1;
  public runRowsSearchText = '';
  public selectedRunRowId: string | null = null;
  public isLoadingRunRows = false;

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const evalConfigId = params.get('id');
      const evalRunId = params.get('runId');
      if (!evalConfigId || !evalRunId) {
        void this.router.navigate(['/evaluations']);
        return;
      }

      this.evalConfigId = evalConfigId;
      this.evalRunId = evalRunId;
      this.runRowsPage = 1;
      this.selectedRunRowId = null;
      this.runRowsSearchText = '';
      this.loadRunDetail(true);
    });
  }

  ngOnDestroy(): void {
    if (this.runRowsSearchDebounceId) {
      clearTimeout(this.runRowsSearchDebounceId);
      this.runRowsSearchDebounceId = undefined;
    }
  }

  runRowsPageCount(): number {
    return Math.ceil(this.runRowsTotal / this.runRowsPageSize);
  }

  runRowsPageNumbers(): number[] {
    return this.buildPageNumbers(this.runRowsPage, this.runRowsPageCount());
  }

  onRunRowsPageChange(page: number): void {
    const totalPages = this.runRowsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.runRowsPage) {
      return;
    }

    this.loadRunRowsPage(page);
  }

  onRunRowsSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    this.runRowsSearchText = input?.value ?? '';
    this.scheduleRunRowsSearch();
  }

  applyRunRowsSearch(): void {
    if (this.runRowsSearchDebounceId) {
      clearTimeout(this.runRowsSearchDebounceId);
      this.runRowsSearchDebounceId = undefined;
    }

    this.runRowsPage = 1;
    this.refreshRunRows(true);
  }

  selectRunRow(row: EvalRunRowDetailSnapshot): void {
    this.selectedRunRowId = row.evalRunRowId;
  }

  isRunRowSelected(row: EvalRunRowDetailSnapshot): boolean {
    return this.selectedRunRowId === row.evalRunRowId;
  }

  get selectedRunRow(): EvalRunRowDetailSnapshot | null {
    if (!this.selectedRunRowId) {
      return null;
    }

    return (
      this.runRows.find((row) => row.evalRunRowId === this.selectedRunRowId) ??
      null
    );
  }

  get selectedRunRowGraders(): EvalRunRowGraderDetailSnapshot[] {
    return this.selectedRunRow?.graders ?? [];
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

  private loadRunDetail(resetRows: boolean): void {
    if (!this.evalRunId) {
      return;
    }

    this.isLoadingRunDetail = true;
    this.serverRepository.getEvalRunDetail(this.evalRunId).subscribe((detail) => {
      this.isLoadingRunDetail = false;
      this.runDetail = detail;

      if (!detail) {
        this.runRows = [];
        this.runRowsTotal = 0;
        this.selectedRunRowId = null;
        return;
      }

      this.refreshRunRows(resetRows);
    });
  }

  private refreshRunRows(resetPage: boolean): void {
    if (!this.evalRunId) {
      return;
    }

    this.isLoadingRunRows = true;
    const search = this.runRowsSearchText.trim();
    const targetPage = resetPage ? 1 : this.runRowsPage;
    this.serverRepository
      .getEvalRunRowCount(this.evalRunId, search)
      .subscribe((total) => {
        this.runRowsTotal = total;
        const totalPages = this.runRowsPageCount();
        const nextPage = totalPages === 0 ? 1 : Math.min(targetPage, totalPages);
        this.loadRunRowsPage(nextPage);
      });
  }

  private loadRunRowsPage(page: number): void {
    if (!this.evalRunId) {
      this.runRows = [];
      this.runRowsTotal = 0;
      this.runRowsPage = 1;
      this.selectedRunRowId = null;
      this.isLoadingRunRows = false;
      return;
    }

    this.isLoadingRunRows = true;
    const skip = (page - 1) * this.runRowsPageSize;
    const search = this.runRowsSearchText.trim();
    this.serverRepository
      .getEvalRunRows(
        this.evalRunId,
        search,
        skip,
        this.runRowsPageSize,
        EvalRunRowSortField.Name,
        SortDirection.Ascending,
      )
      .subscribe((rows) => {
        this.runRows = rows;
        this.runRowsPage = page;
        this.isLoadingRunRows = false;
        this.selectActiveRunRowAfterRefresh();
      });
  }

  private selectActiveRunRowAfterRefresh(): void {
    if (this.runRows.length === 0) {
      this.selectedRunRowId = null;
      return;
    }

    if (
      this.selectedRunRowId &&
      this.runRows.some((row) => row.evalRunRowId === this.selectedRunRowId)
    ) {
      return;
    }

    this.selectedRunRowId = this.runRows[0].evalRunRowId;
  }

  private scheduleRunRowsSearch(): void {
    if (this.runRowsSearchDebounceId) {
      clearTimeout(this.runRowsSearchDebounceId);
    }

    this.runRowsSearchDebounceId = setTimeout(
      () => this.applyRunRowsSearch(),
      250,
    );
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
}
