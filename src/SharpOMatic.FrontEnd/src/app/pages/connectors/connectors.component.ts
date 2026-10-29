import {
  AfterViewInit,
  Component,
  ElementRef,
  ViewChild,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { Router, RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ConnectorSummary } from '../../metadata/definitions/connector-summary';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { Connector } from '../../metadata/definitions/connector';
import { ConnectorSortField } from '../../enumerations/connector-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';

@Component({
  selector: 'app-connectors',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './connectors.component.html',
  styleUrls: ['./connectors.component.scss'],
  providers: [BsModalService],
})
export class ConnectorsComponent implements AfterViewInit {
  private readonly serverWorkflow = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  public connectors: ConnectorSummary[] = [];
  public connectorsPage = 1;
  public connectorsTotal = 0;
  public readonly connectorsPageSize = 50;
  public isLoading = true;
  public searchText = '';
  public selectedConnectorIds = new Set<string>();
  public isDeletingSelected = false;
  public connectorsSortField: ConnectorSortField = ConnectorSortField.Name;
  public connectorsSortDirection: SortDirection = SortDirection.Ascending;
  public readonly ConnectorSortField = ConnectorSortField;
  public readonly SortDirection = SortDirection;
  private searchDebounceId: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.refreshConnectors();
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.searchInput?.nativeElement.focus(), 0);
  }

  newConnector(): void {
    const newConnector = new Connector({
      ...Connector.defaultSnapshot(),
      configId: '',
      name: 'Untitled',
      description: 'Connector needs a description.',
    });

    this.serverWorkflow.upsertConnector(newConnector).subscribe(() => {
      this.router.navigate(['/connectors', newConnector.connectorId]);
    });
  }

  selectedConnectorCount(): number {
    return this.selectedConnectorIds.size;
  }

  isConnectorSelected(connectorId: string): boolean {
    return this.selectedConnectorIds.has(connectorId);
  }

  onConnectorSelectionChange(connectorId: string, event: Event): void {
    const input = event.target as HTMLInputElement | null;
    if (input?.checked) {
      this.selectedConnectorIds.add(connectorId);
      return;
    }

    this.selectedConnectorIds.delete(connectorId);
  }

  deleteSelectedConnectors(): void {
    if (this.selectedConnectorIds.size === 0 || this.isDeletingSelected) {
      return;
    }

    const selectedConnectors = this.connectors.filter((connector) =>
      this.selectedConnectorIds.has(connector.connectorId),
    );
    if (selectedConnectors.length === 0) {
      this.clearSelectedConnectors();
      return;
    }

    const count = selectedConnectors.length;
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Connectors',
        message: `Are you sure you want to delete ${count} selected connector${count === 1 ? '' : 's'}?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (!this.confirmModalRef?.content?.result) {
        return;
      }

      this.isDeletingSelected = true;
      forkJoin(
        selectedConnectors.map((connector) =>
          this.serverWorkflow.deleteConnector(connector.connectorId),
        ),
      )
        .pipe(
          finalize(() => {
            this.isDeletingSelected = false;
            this.clearSelectedConnectors();
            this.refreshConnectors();
          }),
        )
        .subscribe();
    });
  }

  connectorsPageCount(): number {
    return Math.ceil(this.connectorsTotal / this.connectorsPageSize);
  }

  connectorsPageNumbers(): number[] {
    const totalPages = this.connectorsPageCount();
    if (totalPages <= 1) {
      return [];
    }

    const currentPage = this.connectorsPage;
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

  onConnectorsPageChange(page: number): void {
    const totalPages = this.connectorsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.connectorsPage) {
      return;
    }

    this.clearSelectedConnectors();
    this.loadConnectorsPage(page);
  }

  onConnectorsSortChange(field: ConnectorSortField): void {
    if (this.connectorsSortField === field) {
      this.connectorsSortDirection =
        this.connectorsSortDirection === SortDirection.Descending
          ? SortDirection.Ascending
          : SortDirection.Descending;
    } else {
      this.connectorsSortField = field;
      this.connectorsSortDirection = SortDirection.Ascending;
    }

    this.clearSelectedConnectors();
    this.connectorsPage = 1;
    this.refreshConnectors();
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

    this.clearSelectedConnectors();
    this.connectorsPage = 1;
    this.refreshConnectors();
  }

  private refreshConnectors(): void {
    this.isLoading = true;
    const search = this.searchText.trim();
    this.serverWorkflow.getConnectorCount(search).subscribe((total) => {
      this.connectorsTotal = total;
      const totalPages = this.connectorsPageCount();
      const nextPage =
        totalPages === 0 ? 1 : Math.min(this.connectorsPage, totalPages);
      this.loadConnectorsPage(nextPage);
    });
  }

  private loadConnectorsPage(page: number): void {
    this.isLoading = true;
    const skip = (page - 1) * this.connectorsPageSize;
    const search = this.searchText.trim();
    this.serverWorkflow
      .getConnectorSummaries(
        search,
        skip,
        this.connectorsPageSize,
        this.connectorsSortField,
        this.connectorsSortDirection,
      )
      .subscribe({
        next: (connectors) => {
          this.connectors = connectors;
          this.syncSelectedConnectorsWithPage();
          this.connectorsPage = page;
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

  private clearSelectedConnectors(): void {
    this.selectedConnectorIds.clear();
  }

  private syncSelectedConnectorsWithPage(): void {
    if (this.selectedConnectorIds.size === 0) {
      return;
    }

    const visibleIds = new Set(
      this.connectors.map((connector) => connector.connectorId),
    );
    for (const selectedId of Array.from(this.selectedConnectorIds)) {
      if (!visibleIds.has(selectedId)) {
        this.selectedConnectorIds.delete(selectedId);
      }
    }
  }
}
