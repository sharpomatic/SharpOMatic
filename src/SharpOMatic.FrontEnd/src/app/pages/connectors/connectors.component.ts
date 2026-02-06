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
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ConnectorSummary } from '../../metadata/definitions/connector-summary';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { Connector } from '../../metadata/definitions/connector';

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

  deleteConnector(connector: ConnectorSummary) {
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Connector',
        message: `Are you sure you want to delete the connector '${connector.name}'?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (this.confirmModalRef?.content?.result) {
        this.serverWorkflow
          .deleteConnector(connector.connectorId)
          .subscribe(() => {
            this.refreshConnectors();
          });
      }
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

    this.loadConnectorsPage(page);
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
      .getConnectorSummaries(search, skip, this.connectorsPageSize)
      .subscribe({
        next: (connectors) => {
          this.connectors = connectors;
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
}
