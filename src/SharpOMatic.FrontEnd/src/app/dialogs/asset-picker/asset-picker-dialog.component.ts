import { CommonModule, DatePipe } from '@angular/common';
import { Component, EventEmitter, Inject, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AssetRef } from '../../entities/definitions/asset-ref';
import { AssetScope } from '../../enumerations/asset-scope';
import { AssetSortField } from '../../enumerations/asset-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';
import { formatByteSize } from '../../helper/format-size';
import { AssetSummary } from '../../pages/assets/interfaces/asset-summary';
import { AssetFolderSummary } from '../../pages/assets/interfaces/asset-folder-summary';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { DIALOG_DATA } from '../services/dialog.service';

export interface AssetPickerDialogOptions {
  mode: 'single' | 'multi';
  title?: string;
  initialSelection?: AssetRef[];
  allowStack?: boolean;
  onSelect?: (assets: AssetRef[]) => void;
}

@Component({
  selector: 'app-asset-picker-dialog',
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './asset-picker-dialog.component.html',
  styleUrls: ['./asset-picker-dialog.component.scss'],
})
export class AssetPickerDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();

  public assets: AssetSummary[] = [];
  public folders: AssetFolderSummary[] = [];
  public selectedFolderFilter: string = 'all';
  public assetsPage = 1;
  public assetsTotal = 0;
  public readonly assetsPageSize = 50;
  public isLoading = false;
  public readonly AssetSortField = AssetSortField;
  public readonly SortDirection = SortDirection;
  public assetsSortField: AssetSortField = AssetSortField.Name;
  public assetsSortDirection: SortDirection = SortDirection.Descending;
  public searchText = '';
  public readonly isMulti: boolean;
  public readonly title: string;

  private readonly selectedAssetsById = new Map<string, AssetRef>();
  private readonly onSelect?: (assets: AssetRef[]) => void;
  private searchDebounceId: ReturnType<typeof setTimeout> | undefined;

  constructor(
    @Inject(DIALOG_DATA) data: AssetPickerDialogOptions,
    private readonly serverRepository: ServerRepositoryService,
  ) {
    this.isMulti = data.mode === 'multi';
    this.title =
      data.title ?? (this.isMulti ? 'Select assets' : 'Select asset');
    this.onSelect = data.onSelect;

    (data.initialSelection ?? []).forEach((asset) => {
      this.selectedAssetsById.set(asset.assetId, asset);
    });
  }

  ngOnInit(): void {
    this.refreshFolders();
    this.refreshAssets();
  }

  onCancel(): void {
    this.close.emit();
  }

  onRowDoubleClick(asset: AssetSummary): void {
    if (this.isMulti) {
      return;
    }

    this.emitSelection([this.toAssetRef(asset)]);
  }

  onAssetCheckboxChange(event: Event, asset: AssetSummary): void {
    if (!this.isMulti) {
      return;
    }

    const input = event.target as HTMLInputElement | null;
    const checked = input?.checked ?? false;
    if (checked) {
      this.selectedAssetsById.set(asset.assetId, this.toAssetRef(asset));
    } else {
      this.selectedAssetsById.delete(asset.assetId);
    }
  }

  confirmSelection(): void {
    this.emitSelection(Array.from(this.selectedAssetsById.values()));
  }

  isAssetSelected(assetId: string): boolean {
    return this.selectedAssetsById.has(assetId);
  }

  formatSize(bytes: number): string {
    return formatByteSize(bytes);
  }

  assetsPageCount(): number {
    return Math.ceil(this.assetsTotal / this.assetsPageSize);
  }

  assetsPageNumbers(): number[] {
    const totalPages = this.assetsPageCount();
    if (totalPages <= 1) {
      return [];
    }

    const currentPage = this.assetsPage;
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

  onAssetsSortChange(field: AssetSortField): void {
    if (this.assetsSortField === field) {
      this.assetsSortDirection =
        this.assetsSortDirection === SortDirection.Descending
          ? SortDirection.Ascending
          : SortDirection.Descending;
    } else {
      this.assetsSortField = field;
      this.assetsSortDirection = SortDirection.Descending;
    }

    this.assetsPage = 1;
    this.refreshAssets();
  }

  onFolderFilterChange(event: Event): void {
    const input = event.target as HTMLSelectElement | null;
    this.selectedFolderFilter = input?.value ?? 'all';
    this.assetsPage = 1;
    this.refreshAssets();
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

    this.assetsPage = 1;
    this.refreshAssets();
  }

  private refreshAssets(): void {
    this.isLoading = true;
    const search = this.searchText.trim();
    const folderId = this.currentFolderId();
    const topLevelOnly = this.selectedFolderFilter === 'top';
    this.serverRepository
      .getAssetsCount(
        AssetScope.Library,
        search,
        undefined,
        folderId ?? undefined,
        topLevelOnly,
      )
      .subscribe((total) => {
        this.assetsTotal = total;
        const totalPages = this.assetsPageCount();
        const nextPage =
          totalPages === 0 ? 1 : Math.min(this.assetsPage, totalPages);
        this.loadAssetsPage(nextPage);
      });
  }

  private loadAssetsPage(page: number): void {
    this.isLoading = true;
    const skip = (page - 1) * this.assetsPageSize;
    const search = this.searchText.trim();
    const folderId = this.currentFolderId();
    const topLevelOnly = this.selectedFolderFilter === 'top';
    this.serverRepository
      .getAssets(
        AssetScope.Library,
        skip,
        this.assetsPageSize,
        this.assetsSortField,
        this.assetsSortDirection,
        search,
        undefined,
        folderId ?? undefined,
        topLevelOnly,
      )
      .subscribe((assets) => {
        this.assets = assets;
        this.assetsPage = page;
        this.isLoading = false;
      });
  }

  private scheduleSearch(): void {
    if (this.searchDebounceId) {
      clearTimeout(this.searchDebounceId);
    }

    this.searchDebounceId = setTimeout(() => this.applySearch(), 250);
  }

  public showFolderColumn(): boolean {
    return this.selectedFolderFilter === 'all';
  }

  private refreshFolders(): void {
    this.serverRepository
      .getAssetFolders('', 0, 0, SortDirection.Ascending)
      .subscribe((folders) => {
        this.folders = folders;
        if (
          this.selectedFolderFilter !== 'all' &&
          this.selectedFolderFilter !== 'top' &&
          !this.folders.some(
            (folder) => folder.folderId === this.selectedFolderFilter,
          )
        ) {
          this.selectedFolderFilter = 'all';
        }
      });
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

  private emitSelection(assets: AssetRef[]): void {
    this.onSelect?.(assets);
    this.close.emit();
  }

  private toAssetRef(asset: AssetSummary): AssetRef {
    return {
      assetId: asset.assetId,
      name: asset.name,
      mediaType: asset.mediaType,
      sizeBytes: asset.sizeBytes,
      folderId: asset.folderId ?? null,
      folderName: asset.folderName ?? null,
    };
  }
}
