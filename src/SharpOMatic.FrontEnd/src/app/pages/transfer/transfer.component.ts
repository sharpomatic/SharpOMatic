import { CommonModule } from '@angular/common';
import { HttpResponse } from '@angular/common/http';
import { Component, OnInit, TemplateRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { InformationDialogComponent } from '../../dialogs/information/information-dialog.component';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ToastService } from '../../services/toast.service';
import { TransferImportResult } from './interfaces/transfer-import-result';
import { TransferExportRequest } from './interfaces/transfer-export-request';
import { TransferSelection } from './interfaces/transfer-selection';
import { WorkflowSummaryEntity } from '../../entities/definitions/workflow.summary.entity';
import { ConnectorSummary } from '../../metadata/definitions/connector-summary';
import { ModelSummary } from '../../metadata/definitions/model-summary';
import { AssetSummary } from '../assets/interfaces/asset-summary';
import { AssetScope } from '../../enumerations/asset-scope';
import { AssetSortField } from '../../enumerations/asset-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';
import { WorkflowSortField } from '../../enumerations/workflow-sort-field';
import { ConnectorSortField } from '../../enumerations/connector-sort-field';
import { ModelSortField } from '../../enumerations/model-sort-field';
import { formatByteSize } from '../../helper/format-size';

type SelectionMode = 'all' | 'none' | 'custom';

@Component({
  selector: 'app-transfer',
  standalone: true,
  imports: [CommonModule, FormsModule, TabComponent],
  templateUrl: './transfer.component.html',
  styleUrls: ['./transfer.component.scss'],
  providers: [BsModalService],
})
export class TransferComponent implements OnInit {
  @ViewChild('importTab', { static: true }) importTab!: TemplateRef<unknown>;
  @ViewChild('exportTab', { static: true }) exportTab!: TemplateRef<unknown>;
  @ViewChild('workflowsExportTab', { static: true }) workflowsExportTab!: TemplateRef<unknown>;
  @ViewChild('connectorsExportTab', { static: true }) connectorsExportTab!: TemplateRef<unknown>;
  @ViewChild('modelsExportTab', { static: true }) modelsExportTab!: TemplateRef<unknown>;
  @ViewChild('assetsExportTab', { static: true }) assetsExportTab!: TemplateRef<unknown>;

  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly toastService = inject(ToastService);
  private infoModalRef: BsModalRef<InformationDialogComponent> | undefined;

  public tabs: TabItem[] = [];
  public activeTabId = 'import';
  public exportTabs: TabItem[] = [];
  public exportActiveTabId = 'workflows';
  public isImporting = false;
  public selectedFileName = '';
  public isExporting = false;
  public includeSecrets = false;

  public workflows: WorkflowSummaryEntity[] = [];
  public connectors: ConnectorSummary[] = [];
  public models: ModelSummary[] = [];
  public assets: AssetSummary[] = [];
  public workflowsTotal = 0;
  public workflowsFilteredTotal = 0;
  public connectorsTotal = 0;
  public connectorsFilteredTotal = 0;
  public modelsTotal = 0;
  public modelsFilteredTotal = 0;
  public assetsTotal = 0;
  public assetsFilteredTotal = 0;
  public readonly exportPageSize = 50;

  public workflowsMode: SelectionMode = 'all';
  public connectorsMode: SelectionMode = 'all';
  public modelsMode: SelectionMode = 'all';
  public assetsMode: SelectionMode = 'all';
  public workflowsPage = 1;
  public connectorsPage = 1;
  public modelsPage = 1;
  public assetsPage = 1;
  public workflowsSortField: WorkflowSortField = WorkflowSortField.Name;
  public workflowsSortDirection: SortDirection = SortDirection.Ascending;
  public connectorsSortField: ConnectorSortField = ConnectorSortField.Name;
  public connectorsSortDirection: SortDirection = SortDirection.Ascending;
  public modelsSortField: ModelSortField = ModelSortField.Name;
  public modelsSortDirection: SortDirection = SortDirection.Ascending;
  public assetsSortField: AssetSortField = AssetSortField.Name;
  public assetsSortDirection: SortDirection = SortDirection.Ascending;
  public workflowsSearchText = '';
  public connectorsSearchText = '';
  public modelsSearchText = '';
  public assetsSearchText = '';

  public readonly WorkflowSortField = WorkflowSortField;
  public readonly ConnectorSortField = ConnectorSortField;
  public readonly ModelSortField = ModelSortField;
  public readonly AssetSortField = AssetSortField;
  public readonly SortDirection = SortDirection;

  public selectedWorkflows = new Set<string>();
  public selectedConnectors = new Set<string>();
  public selectedModels = new Set<string>();
  public selectedAssets = new Set<string>();

  private workflowsSearchDebounceId: ReturnType<typeof setTimeout> | undefined;
  private connectorsSearchDebounceId: ReturnType<typeof setTimeout> | undefined;
  private modelsSearchDebounceId: ReturnType<typeof setTimeout> | undefined;
  private assetsSearchDebounceId: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.tabs = [
      { id: 'import', title: 'Import', content: this.importTab },
      { id: 'export', title: 'Export', content: this.exportTab },
    ];
    this.updateExportTabs();

    this.loadExportLists();
  }

  onActiveTabIdChange(tabId: string): void {
    this.activeTabId = tabId;
  }

  onExportTabIdChange(tabId: string): void {
    this.exportActiveTabId = tabId;
  }

  exportTransfer(): void {
    if (this.isExporting || !this.canExport()) {
      return;
    }

    this.isExporting = true;
    const request = this.buildExportRequest();

    this.serverRepository.exportTransfer(request).subscribe({
      next: (response) => {
        this.isExporting = false;
        this.downloadExport(response);
      },
      error: (error) => {
        this.isExporting = false;
        this.showExportFailure(error);
      }
    });
  }

  triggerImport(fileInput: HTMLInputElement): void {
    if (this.isImporting) {
      return;
    }

    fileInput.click();
  }

  onImportFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0];
    if (!file || this.isImporting) {
      if (input) {
        input.value = '';
      }
      return;
    }

    this.selectedFileName = file.name;
    this.isImporting = true;

    const resetInput = () => {
      if (input) {
        input.value = '';
      }
    };

    this.serverRepository.importTransfer(file).subscribe({
      next: (result) => {
        this.isImporting = false;
        resetInput();
        this.showImportSuccess(result);
      },
      error: (error) => {
        this.isImporting = false;
        resetInput();
        this.showImportFailure(error);
      }
    });
  }

  private showImportSuccess(result: TransferImportResult): void {
    const message = `Imported ${result.workflowsImported} workflows, ` +
      `${result.connectorsImported} connectors, ${result.modelsImported} models, ` +
      `${result.assetsImported} assets.`;
    this.showInfoDialog('Import Complete', message);
  }

  private showImportFailure(error: unknown): void {
    const detail = this.toastService.extractErrorDetail(error);
    const message = detail ? `Import failed: ${detail}` : 'Import failed.';
    this.showInfoDialog('Import Failed', message);
  }

  private showExportFailure(error: unknown): void {
    if (typeof error === 'string') {
      this.showInfoDialog('Export Failed', error);
      return;
    }

    const detail = this.toastService.extractErrorDetail(error);
    const message = detail ? `Export failed: ${detail}` : 'Export failed.';
    this.showInfoDialog('Export Failed', message);
  }

  onSelectionChange(selected: Set<string>, id: string, event: Event): void {
    const input = event.target as HTMLInputElement | null;
    if (!input) {
      return;
    }

    if (input.checked) {
      selected.add(id);
    } else {
      selected.delete(id);
    }

    this.updateExportTabs();
  }

  formatSize(bytes: number): string {
    return formatByteSize(bytes);
  }

  workflowsPageCount(): number {
    return Math.ceil(this.workflowsFilteredTotal / this.exportPageSize);
  }

  connectorsPageCount(): number {
    return Math.ceil(this.connectorsFilteredTotal / this.exportPageSize);
  }

  modelsPageCount(): number {
    return Math.ceil(this.modelsFilteredTotal / this.exportPageSize);
  }

  assetsPageCount(): number {
    return Math.ceil(this.assetsFilteredTotal / this.exportPageSize);
  }

  workflowsPageNumbers(): number[] {
    return this.buildPageNumbers(this.workflowsPage, this.workflowsPageCount());
  }

  connectorsPageNumbers(): number[] {
    return this.buildPageNumbers(this.connectorsPage, this.connectorsPageCount());
  }

  modelsPageNumbers(): number[] {
    return this.buildPageNumbers(this.modelsPage, this.modelsPageCount());
  }

  assetsPageNumbers(): number[] {
    return this.buildPageNumbers(this.assetsPage, this.assetsPageCount());
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

  onModelsPageChange(page: number): void {
    const totalPages = this.modelsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.modelsPage) {
      return;
    }

    this.loadModelsPage(page);
  }

  onAssetsPageChange(page: number): void {
    const totalPages = this.assetsPageCount();
    if (page < 1 || (totalPages > 0 && page > totalPages)) {
      return;
    }

    if (page === this.assetsPage) {
      return;
    }

    this.loadAssetsPage(page);
  }

  onWorkflowsSortChange(field: WorkflowSortField): void {
    if (this.workflowsSortField === field) {
      this.workflowsSortDirection = this.workflowsSortDirection === SortDirection.Descending
        ? SortDirection.Ascending
        : SortDirection.Descending;
    } else {
      this.workflowsSortField = field;
      this.workflowsSortDirection = SortDirection.Ascending;
    }

    this.workflowsPage = 1;
    this.loadWorkflowsPage(1);
  }

  onConnectorsSortChange(field: ConnectorSortField): void {
    if (this.connectorsSortField === field) {
      this.connectorsSortDirection = this.connectorsSortDirection === SortDirection.Descending
        ? SortDirection.Ascending
        : SortDirection.Descending;
    } else {
      this.connectorsSortField = field;
      this.connectorsSortDirection = SortDirection.Ascending;
    }

    this.connectorsPage = 1;
    this.loadConnectorsPage(1);
  }

  onModelsSortChange(field: ModelSortField): void {
    if (this.modelsSortField === field) {
      this.modelsSortDirection = this.modelsSortDirection === SortDirection.Descending
        ? SortDirection.Ascending
        : SortDirection.Descending;
    } else {
      this.modelsSortField = field;
      this.modelsSortDirection = SortDirection.Ascending;
    }

    this.modelsPage = 1;
    this.loadModelsPage(1);
  }

  onAssetsSortChange(field: AssetSortField): void {
    if (this.assetsSortField === field) {
      this.assetsSortDirection = this.assetsSortDirection === SortDirection.Descending
        ? SortDirection.Ascending
        : SortDirection.Descending;
    } else {
      this.assetsSortField = field;
      this.assetsSortDirection = SortDirection.Ascending;
    }

    this.assetsPage = 1;
    this.loadAssetsPage(1);
  }

  onWorkflowsSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    this.workflowsSearchText = input?.value ?? '';
    this.scheduleWorkflowsSearch();
  }

  onConnectorsSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    this.connectorsSearchText = input?.value ?? '';
    this.scheduleConnectorsSearch();
  }

  onModelsSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    this.modelsSearchText = input?.value ?? '';
    this.scheduleModelsSearch();
  }

  onAssetsSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    this.assetsSearchText = input?.value ?? '';
    this.scheduleAssetsSearch();
  }

  applyWorkflowsSearch(): void {
    if (this.workflowsSearchDebounceId) {
      clearTimeout(this.workflowsSearchDebounceId);
      this.workflowsSearchDebounceId = undefined;
    }

    this.workflowsPage = 1;
    this.refreshWorkflows();
  }

  applyConnectorsSearch(): void {
    if (this.connectorsSearchDebounceId) {
      clearTimeout(this.connectorsSearchDebounceId);
      this.connectorsSearchDebounceId = undefined;
    }

    this.connectorsPage = 1;
    this.refreshConnectors();
  }

  applyModelsSearch(): void {
    if (this.modelsSearchDebounceId) {
      clearTimeout(this.modelsSearchDebounceId);
      this.modelsSearchDebounceId = undefined;
    }

    this.modelsPage = 1;
    this.refreshModels();
  }

  applyAssetsSearch(): void {
    if (this.assetsSearchDebounceId) {
      clearTimeout(this.assetsSearchDebounceId);
      this.assetsSearchDebounceId = undefined;
    }

    this.assetsPage = 1;
    this.refreshAssets();
  }

  canExport(): boolean {
    return this.hasSelection(this.workflowsMode, this.selectedWorkflows) ||
      this.hasSelection(this.connectorsMode, this.selectedConnectors) ||
      this.hasSelection(this.modelsMode, this.selectedModels) ||
      this.hasSelection(this.assetsMode, this.selectedAssets);
  }

  private hasSelection(mode: SelectionMode, selected: Set<string>): boolean {
    if (mode === 'all') {
      return true;
    }

    if (mode === 'custom') {
      return selected.size > 0;
    }

    return false;
  }

  private loadExportLists(): void {
    this.refreshWorkflows();
    this.refreshConnectors();
    this.refreshModels();
    this.refreshAssets();
  }

  private buildExportRequest(): TransferExportRequest {
    const request: TransferExportRequest = {
      includeSecrets: this.includeSecrets,
    };

    const workflows = this.buildSelection(this.workflowsMode, this.selectedWorkflows);
    if (workflows) {
      request.workflows = workflows;
    }

    const connectors = this.buildSelection(this.connectorsMode, this.selectedConnectors);
    if (connectors) {
      request.connectors = connectors;
    }

    const models = this.buildSelection(this.modelsMode, this.selectedModels);
    if (models) {
      request.models = models;
    }

    const assets = this.buildSelection(this.assetsMode, this.selectedAssets);
    if (assets) {
      request.assets = assets;
    }

    return request;
  }

  private buildSelection(mode: SelectionMode, selected: Set<string>): TransferSelection | null {
    if (mode === 'all') {
      return { all: true, ids: [] };
    }

    if (mode === 'custom') {
      return { all: false, ids: Array.from(selected) };
    }

    return null;
  }

  private downloadExport(response: HttpResponse<Blob>): void {
    if (!response.body) {
      this.showExportFailure('Export failed: empty export response.');
      return;
    }

    const header = response.headers.get('content-disposition');
    const fileName = this.parseFileName(header) ?? 'sharpomatic-export.zip';

    const url = window.URL.createObjectURL(response.body);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  }

  private parseFileName(contentDisposition: string | null): string | null {
    if (!contentDisposition) {
      return null;
    }

    const fileNameStar = /filename\*\s*=\s*(?:UTF-8'')?([^;]+)/i.exec(contentDisposition);
    if (fileNameStar?.[1]) {
      return decodeURIComponent(fileNameStar[1].trim().replace(/^"|"$/g, ''));
    }

    const fileNameMatch = /filename\s*=\s*([^;]+)/i.exec(contentDisposition);
    if (fileNameMatch?.[1]) {
      return fileNameMatch[1].trim().replace(/^"|"$/g, '');
    }

    return null;
  }

  public updateExportTabs(): void {
    this.exportTabs = [
      {
        id: 'workflows',
        title: this.exportTabTitle('Workflows', this.workflowsMode, this.selectedWorkflows, this.workflowsTotal),
        content: this.workflowsExportTab,
      },
      {
        id: 'connectors',
        title: this.exportTabTitle('Connectors', this.connectorsMode, this.selectedConnectors, this.connectorsTotal),
        content: this.connectorsExportTab,
      },
      {
        id: 'models',
        title: this.exportTabTitle('Models', this.modelsMode, this.selectedModels, this.modelsTotal),
        content: this.modelsExportTab,
      },
      {
        id: 'assets',
        title: this.exportTabTitle('Assets', this.assetsMode, this.selectedAssets, this.assetsTotal),
        content: this.assetsExportTab,
      },
    ];
  }

  private exportTabTitle(label: string, mode: SelectionMode, selected: Set<string>, total: number): string {
    return `${label} (${this.selectionCount(mode, selected, total)})`;
  }

  private selectionCount(mode: SelectionMode, selected: Set<string>, total: number): number {
    switch (mode) {
      case 'all':
        return total;
      case 'custom':
        return selected.size;
      default:
        return 0;
    }
  }

  private refreshWorkflows(): void {
    const search = this.workflowsSearchText.trim();
    if (!search) {
      this.serverRepository.getWorkflowCount().subscribe(total => {
        this.workflowsTotal = total;
        this.workflowsFilteredTotal = total;
        const totalPages = this.workflowsPageCount();
        const nextPage = totalPages === 0 ? 1 : Math.min(this.workflowsPage, totalPages);
        this.loadWorkflowsPage(nextPage);
        this.updateExportTabs();
      });
      return;
    }

    this.serverRepository.getWorkflowCount().subscribe(total => {
      this.workflowsTotal = total;
      this.updateExportTabs();
    });

    this.serverRepository.getWorkflowCount(search).subscribe(total => {
      this.workflowsFilteredTotal = total;
      const totalPages = this.workflowsPageCount();
      const nextPage = totalPages === 0 ? 1 : Math.min(this.workflowsPage, totalPages);
      this.loadWorkflowsPage(nextPage);
    });
  }

  private refreshConnectors(): void {
    const search = this.connectorsSearchText.trim();
    if (!search) {
      this.serverRepository.getConnectorCount().subscribe(total => {
        this.connectorsTotal = total;
        this.connectorsFilteredTotal = total;
        const totalPages = this.connectorsPageCount();
        const nextPage = totalPages === 0 ? 1 : Math.min(this.connectorsPage, totalPages);
        this.loadConnectorsPage(nextPage);
        this.updateExportTabs();
      });
      return;
    }

    this.serverRepository.getConnectorCount().subscribe(total => {
      this.connectorsTotal = total;
      this.updateExportTabs();
    });

    this.serverRepository.getConnectorCount(search).subscribe(total => {
      this.connectorsFilteredTotal = total;
      const totalPages = this.connectorsPageCount();
      const nextPage = totalPages === 0 ? 1 : Math.min(this.connectorsPage, totalPages);
      this.loadConnectorsPage(nextPage);
    });
  }

  private refreshModels(): void {
    const search = this.modelsSearchText.trim();
    if (!search) {
      this.serverRepository.getModelCount().subscribe(total => {
        this.modelsTotal = total;
        this.modelsFilteredTotal = total;
        const totalPages = this.modelsPageCount();
        const nextPage = totalPages === 0 ? 1 : Math.min(this.modelsPage, totalPages);
        this.loadModelsPage(nextPage);
        this.updateExportTabs();
      });
      return;
    }

    this.serverRepository.getModelCount().subscribe(total => {
      this.modelsTotal = total;
      this.updateExportTabs();
    });

    this.serverRepository.getModelCount(search).subscribe(total => {
      this.modelsFilteredTotal = total;
      const totalPages = this.modelsPageCount();
      const nextPage = totalPages === 0 ? 1 : Math.min(this.modelsPage, totalPages);
      this.loadModelsPage(nextPage);
    });
  }

  private refreshAssets(): void {
    const search = this.assetsSearchText.trim();
    if (!search) {
      this.serverRepository.getAssetsCount(AssetScope.Library).subscribe(total => {
        this.assetsTotal = total;
        this.assetsFilteredTotal = total;
        const totalPages = this.assetsPageCount();
        const nextPage = totalPages === 0 ? 1 : Math.min(this.assetsPage, totalPages);
        this.loadAssetsPage(nextPage);
        this.updateExportTabs();
      });
      return;
    }

    this.serverRepository.getAssetsCount(AssetScope.Library).subscribe(total => {
      this.assetsTotal = total;
      this.updateExportTabs();
    });

    this.serverRepository.getAssetsCount(AssetScope.Library, search).subscribe(total => {
      this.assetsFilteredTotal = total;
      const totalPages = this.assetsPageCount();
      const nextPage = totalPages === 0 ? 1 : Math.min(this.assetsPage, totalPages);
      this.loadAssetsPage(nextPage);
    });
  }

  private loadWorkflowsPage(page: number): void {
    const skip = (page - 1) * this.exportPageSize;
    const search = this.workflowsSearchText.trim();
    this.serverRepository.getWorkflows(
      search,
      skip,
      this.exportPageSize,
      this.workflowsSortField,
      this.workflowsSortDirection
    ).subscribe(workflows => {
      this.workflows = workflows;
      this.workflowsPage = page;
    });
  }

  private loadConnectorsPage(page: number): void {
    const skip = (page - 1) * this.exportPageSize;
    const search = this.connectorsSearchText.trim();
    this.serverRepository.getConnectorSummaries(
      search,
      skip,
      this.exportPageSize,
      this.connectorsSortField,
      this.connectorsSortDirection
    ).subscribe(connectors => {
      this.connectors = connectors;
      this.connectorsPage = page;
    });
  }

  private loadModelsPage(page: number): void {
    const skip = (page - 1) * this.exportPageSize;
    const search = this.modelsSearchText.trim();
    this.serverRepository.getModelSummaries(
      search,
      skip,
      this.exportPageSize,
      this.modelsSortField,
      this.modelsSortDirection
    ).subscribe(models => {
      this.models = models;
      this.modelsPage = page;
    });
  }

  private loadAssetsPage(page: number): void {
    const skip = (page - 1) * this.exportPageSize;
    const search = this.assetsSearchText.trim();
    this.serverRepository.getAssets(
      AssetScope.Library,
      skip,
      this.exportPageSize,
      this.assetsSortField,
      this.assetsSortDirection,
      search
    ).subscribe(assets => {
      this.assets = assets;
      this.assetsPage = page;
    });
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

  private showInfoDialog(title: string, message: string): void {
    this.infoModalRef = this.modalService.show(InformationDialogComponent, {
      initialState: {
        title,
        message
      }
    });
  }

  private scheduleWorkflowsSearch(): void {
    if (this.workflowsSearchDebounceId) {
      clearTimeout(this.workflowsSearchDebounceId);
    }

    this.workflowsSearchDebounceId = setTimeout(() => this.applyWorkflowsSearch(), 250);
  }

  private scheduleConnectorsSearch(): void {
    if (this.connectorsSearchDebounceId) {
      clearTimeout(this.connectorsSearchDebounceId);
    }

    this.connectorsSearchDebounceId = setTimeout(() => this.applyConnectorsSearch(), 250);
  }

  private scheduleModelsSearch(): void {
    if (this.modelsSearchDebounceId) {
      clearTimeout(this.modelsSearchDebounceId);
    }

    this.modelsSearchDebounceId = setTimeout(() => this.applyModelsSearch(), 250);
  }

  private scheduleAssetsSearch(): void {
    if (this.assetsSearchDebounceId) {
      clearTimeout(this.assetsSearchDebounceId);
    }

    this.assetsSearchDebounceId = setTimeout(() => this.applyAssetsSearch(), 250);
  }
}
