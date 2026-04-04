import { CommonModule, DatePipe } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ElementRef,
  ViewChild,
  inject,
} from '@angular/core';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { finalize, forkJoin } from 'rxjs';
import { AssetPreviewDialogComponent } from '../../dialogs/asset-preview/asset-preview-dialog.component';
import { AssetTextDialogComponent } from '../../dialogs/asset-text/asset-text-dialog.component';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { InformationDialogComponent } from '../../dialogs/information/information-dialog.component';
import {
  MoveAssetsDialogComponent,
  MoveAssetsTopLevelOptionValue,
} from '../../dialogs/move-assets/move-assets-dialog.component';
import { TextInputDialogComponent } from '../../dialogs/text-input/text-input-dialog.component';
import { AssetScope } from '../../enumerations/asset-scope';
import { AssetSortField } from '../../enumerations/asset-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';
import { isTextLikeMediaType, normalizeMediaType } from '../../helper/asset-media-type';
import { formatByteSize } from '../../helper/format-size';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { AssetFolderSummary } from './interfaces/asset-folder-summary';
import { AssetSummary } from './interfaces/asset-summary';

@Component({
  selector: 'app-assets',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './assets.component.html',
  styleUrls: ['./assets.component.scss'],
  providers: [BsModalService],
})
export class AssetsComponent implements AfterViewInit {
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly folderOrderStorageKey = 'assets.folder.order';
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  private infoModalRef: BsModalRef<InformationDialogComponent> | undefined;
  private folderNameModalRef: BsModalRef<TextInputDialogComponent> | undefined;
  private moveAssetsModalRef: BsModalRef<MoveAssetsDialogComponent> | undefined;
  private textModalRef: BsModalRef<AssetTextDialogComponent> | undefined;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  public assets: AssetSummary[] = [];
  public folders: AssetFolderSummary[] = [];
  public selectedFolderFilter: string = 'all';
  public assetsPage = 1;
  public assetsTotal = 0;
  public readonly assetsPageSize = 50;
  public isUploading = false;
  public readonly AssetSortField = AssetSortField;
  public readonly SortDirection = SortDirection;
  public assetsSortField: AssetSortField = AssetSortField.Name;
  public assetsSortDirection: SortDirection = SortDirection.Descending;
  public selectedAssetIds = new Set<string>();
  public searchText = '';
  private searchDebounceId: ReturnType<typeof setTimeout> | undefined;
  private folderOrder: string[] = this.readStoredFolderOrder();
  public isDeletingSelected = false;
  public isMovingSelected = false;

  ngOnInit(): void {
    this.refreshFolders();
    this.refreshAssets();
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.searchInput?.nativeElement.focus(), 0);
  }

  triggerUpload(fileInput: HTMLInputElement): void {
    fileInput.click();
  }

  uploadAssets(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const files = input?.files ? Array.from(input.files) : [];
    if (files.length === 0 || this.isUploading) {
      if (input) {
        input.value = '';
      }
      return;
    }

    this.isUploading = true;
    const folderId = this.currentFolderId();
    const uploads = files.map((file) =>
      this.serverRepository.uploadAsset(
        file,
        file.name,
        AssetScope.Library,
        undefined,
        folderId ?? undefined,
      ),
    );
    forkJoin(uploads).subscribe(() => {
      this.isUploading = false;
      this.refreshFolders();
      this.refreshAssets();
      if (input) {
        input.value = '';
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

      this.serverRepository.createAssetFolder(name).subscribe((folder) => {
        if (!folder) {
          return;
        }

        this.selectedFolderFilter = folder.folderId;
        this.refreshFolders();
        this.refreshAssets();
      });
    });
  }

  editSelectedFolder(): void {
    const folderId = this.currentFolderId();
    if (!folderId) {
      return;
    }

    const folder = this.folders.find((entry) => entry.folderId === folderId);
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

      this.serverRepository.renameAssetFolder(folderId, name).subscribe((updated) => {
        if (!updated) {
          return;
        }

        this.refreshFolders();
        this.refreshAssets();
      });
    });
  }

  deleteSelectedFolder(): void {
    const folderId = this.currentFolderId();
    if (!folderId) {
      return;
    }

    const folder = this.folders.find((entry) => entry.folderId === folderId);
    if (!folder) {
      return;
    }

    this.serverRepository
      .getAssetsCount(AssetScope.Library, '', undefined, undefined, folderId, false)
      .subscribe((assetCount) => {
        if (assetCount > 0) {
          this.showInfoDialog(
            'Delete Folder',
            `Folder '${folder.name}' contains ${assetCount} asset${assetCount === 1 ? '' : 's'} and cannot be deleted.`,
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

          this.serverRepository.deleteAssetFolder(folderId).subscribe((success) => {
            if (!success) {
              return;
            }

            this.selectedFolderFilter = 'all';
            this.refreshFolders();
            this.refreshAssets();
          });
        });
      });
  }

  selectAllAssets(): void {
    if (this.selectedFolderFilter === 'all') {
      return;
    }

    this.clearSelectedAssets();
    this.selectedFolderFilter = 'all';
    this.assetsPage = 1;
    this.refreshAssets();
  }

  selectTopLevelAssets(): void {
    if (this.selectedFolderFilter === 'top') {
      return;
    }

    this.clearSelectedAssets();
    this.selectedFolderFilter = 'top';
    this.assetsPage = 1;
    this.refreshAssets();
  }

  selectFolderAssets(folderId: string): void {
    if (this.selectedFolderFilter === folderId) {
      return;
    }

    this.clearSelectedAssets();
    this.selectedFolderFilter = folderId;
    this.assetsPage = 1;
    this.refreshAssets();
  }

  isFolderFilterSelected(folderId: string): boolean {
    return this.selectedFolderFilter === folderId;
  }

  canMoveSelectedFolderUp(): boolean {
    const folderId = this.currentFolderId();
    if (!folderId) {
      return false;
    }

    const index = this.folders.findIndex((folder) => folder.folderId === folderId);
    return index > 0;
  }

  canMoveSelectedFolderDown(): boolean {
    const folderId = this.currentFolderId();
    if (!folderId) {
      return false;
    }

    const index = this.folders.findIndex((folder) => folder.folderId === folderId);
    return index > -1 && index < this.folders.length - 1;
  }

  onMoveSelectedFolderUp(): void {
    const folderId = this.currentFolderId();
    if (!folderId) {
      return;
    }

    const index = this.folders.findIndex((folder) => folder.folderId === folderId);
    if (index <= 0) {
      return;
    }

    const next = [...this.folders];
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    this.folders = next;
    this.persistFolderOrderFromList();
  }

  onMoveSelectedFolderDown(): void {
    const folderId = this.currentFolderId();
    if (!folderId) {
      return;
    }

    const index = this.folders.findIndex((folder) => folder.folderId === folderId);
    if (index < 0 || index >= this.folders.length - 1) {
      return;
    }

    const next = [...this.folders];
    [next[index], next[index + 1]] = [next[index + 1], next[index]];
    this.folders = next;
    this.persistFolderOrderFromList();
  }

  openAssetPreview(asset: AssetSummary): void {
    if (!this.isImageAsset(asset)) {
      return;
    }

    this.modalService.show(AssetPreviewDialogComponent, {
      initialState: {
        assetId: asset.assetId,
        title: asset.name,
        fileName: asset.name,
        imageUrl: this.serverRepository.getAssetContentUrl(asset.assetId),
        altText: asset.name,
      },
      class: 'modal-fullscreen asset-preview-modal',
    });
  }

  isImageAsset(asset: AssetSummary): boolean {
    const mediaType = normalizeMediaType(asset.mediaType);
    return mediaType.startsWith('image/');
  }

  openAssetEditor(asset: AssetSummary): void {
    if (!this.isEditableTextAsset(asset)) {
      return;
    }

    this.textModalRef = this.modalService.show(AssetTextDialogComponent, {
      initialState: {
        assetId: asset.assetId,
        title: asset.name,
      },
      class: 'modal-fullscreen asset-text-modal',
    });

    this.textModalRef.onHidden?.subscribe(() => {
      if (this.textModalRef?.content?.saved) {
        this.refreshAssets();
      }
    });
  }

  isEditableTextAsset(asset: AssetSummary): boolean {
    return isTextLikeMediaType(asset.mediaType);
  }

  formatSize(bytes: number): string {
    return formatByteSize(bytes);
  }

  selectedAssetCount(): number {
    return this.selectedAssetIds.size;
  }

  isAssetSelected(assetId: string): boolean {
    return this.selectedAssetIds.has(assetId);
  }

  onAssetSelectionChange(assetId: string, event: Event): void {
    const input = event.target as HTMLInputElement | null;
    if (input?.checked) {
      this.selectedAssetIds.add(assetId);
      return;
    }

    this.selectedAssetIds.delete(assetId);
  }

  deleteSelectedAssets(): void {
    if (
      this.selectedAssetIds.size === 0 ||
      this.isDeletingSelected ||
      this.isMovingSelected
    ) {
      return;
    }

    const selectedAssets = this.assets.filter((asset) =>
      this.selectedAssetIds.has(asset.assetId),
    );

    if (selectedAssets.length === 0) {
      this.clearSelectedAssets();
      return;
    }

    const count = selectedAssets.length;
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Assets',
        message: `Are you sure you want to delete ${count} selected asset${count === 1 ? '' : 's'}?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (!this.confirmModalRef?.content?.result) {
        return;
      }

      this.isDeletingSelected = true;
      const deleteRequests = selectedAssets.map((asset) =>
        this.serverRepository.deleteAsset(asset.assetId),
      );
      forkJoin(deleteRequests)
        .pipe(
          finalize(() => {
            this.isDeletingSelected = false;
            this.clearSelectedAssets();
            this.refreshFolders();
            this.refreshAssets();
          }),
        )
        .subscribe();
    });
  }

  moveSelectedAssets(): void {
    if (
      this.selectedAssetIds.size === 0 ||
      this.isDeletingSelected ||
      this.isMovingSelected
    ) {
      return;
    }

    const selectedAssets = this.assets.filter((asset) =>
      this.selectedAssetIds.has(asset.assetId),
    );

    if (selectedAssets.length === 0) {
      this.clearSelectedAssets();
      return;
    }

    this.moveAssetsModalRef = this.modalService.show(MoveAssetsDialogComponent, {
      initialState: {
        folders: this.folders.map((folder) => ({
          folderId: folder.folderId,
          name: folder.name,
        })),
        selectedFolderId: null,
      },
    });

    this.moveAssetsModalRef.onHidden?.subscribe(() => {
      const targetFolderValue = this.moveAssetsModalRef?.content?.result;
      if (targetFolderValue === undefined) {
        return;
      }

      const targetFolderId =
        targetFolderValue === MoveAssetsTopLevelOptionValue
          ? null
          : targetFolderValue;

      this.isMovingSelected = true;
      const moveRequests = selectedAssets.map((asset) =>
        this.serverRepository.moveAssetToFolder(asset.assetId, targetFolderId),
      );

      forkJoin(moveRequests)
        .pipe(
          finalize(() => {
            this.isMovingSelected = false;
            this.clearSelectedAssets();
            this.refreshFolders();
            this.refreshAssets();
          }),
        )
        .subscribe();
    });
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

    this.clearSelectedAssets();
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

    this.clearSelectedAssets();
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

    this.clearSelectedAssets();
    this.assetsPage = 1;
    this.refreshAssets();
  }

  clearSearch(): void {
    if (!this.searchText) {
      return;
    }

    this.searchText = '';
    this.applySearch();
  }

  private refreshAssets(): void {
    const search = this.searchText.trim();
    const folderId = this.currentFolderId();
    const topLevelOnly = this.selectedFolderFilter === 'top';
    this.serverRepository
      .getAssetsCount(
        AssetScope.Library,
        search,
        undefined,
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
        undefined,
        folderId ?? undefined,
        topLevelOnly,
      )
      .subscribe((assets) => {
        this.assets = assets;
        this.syncSelectedAssetsWithPage();
        this.assetsPage = page;
      });
  }

  private refreshFolders(): void {
    this.serverRepository
      .getAssetFolders('', 0, 0, SortDirection.Ascending)
      .subscribe((folders) => {
        this.syncFolderOrder(folders);
        this.folders = this.orderFolders(folders);
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

  private clearSelectedAssets(): void {
    this.selectedAssetIds.clear();
  }

  private syncSelectedAssetsWithPage(): void {
    if (this.selectedAssetIds.size === 0) {
      return;
    }

    const visibleIds = new Set(this.assets.map((asset) => asset.assetId));
    for (const selectedId of Array.from(this.selectedAssetIds)) {
      if (!visibleIds.has(selectedId)) {
        this.selectedAssetIds.delete(selectedId);
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

  private orderFolders(folders: AssetFolderSummary[]): AssetFolderSummary[] {
    if (folders.length <= 1) {
      return [...folders];
    }

    const orderLookup = new Map<string, number>();
    this.folderOrder.forEach((folderId, index) => {
      orderLookup.set(folderId, index);
    });

    const ordered = [...folders];
    ordered.sort((a, b) => {
      const aIndex = orderLookup.get(a.folderId);
      const bIndex = orderLookup.get(b.folderId);
      const aHasOrder = aIndex !== undefined;
      const bHasOrder = bIndex !== undefined;

      if (aHasOrder && bHasOrder) {
        return (aIndex as number) - (bIndex as number);
      }

      if (aHasOrder) {
        return -1;
      }

      if (bHasOrder) {
        return 1;
      }

      return a.name.localeCompare(b.name);
    });

    return ordered;
  }

  private syncFolderOrder(folders: AssetFolderSummary[]): void {
    const validIds = new Set(folders.map((folder) => folder.folderId));
    const nextOrder = this.folderOrder.filter((folderId) => validIds.has(folderId));
    const seen = new Set(nextOrder);

    for (const folder of folders) {
      if (seen.has(folder.folderId)) {
        continue;
      }

      nextOrder.push(folder.folderId);
      seen.add(folder.folderId);
    }

    this.folderOrder = nextOrder;
    this.writeStoredFolderOrder(nextOrder);
  }

  private persistFolderOrderFromList(): void {
    this.folderOrder = this.folders.map((folder) => folder.folderId);
    this.writeStoredFolderOrder(this.folderOrder);
  }

  private readStoredFolderOrder(): string[] {
    try {
      const value = localStorage.getItem(this.folderOrderStorageKey);
      if (!value) {
        return [];
      }

      const parsed = JSON.parse(value);
      if (!Array.isArray(parsed)) {
        return [];
      }

      return parsed.filter((entry): entry is string => typeof entry === 'string');
    } catch {
      return [];
    }
  }

  private writeStoredFolderOrder(order: string[]): void {
    try {
      localStorage.setItem(this.folderOrderStorageKey, JSON.stringify(order));
    } catch {}
  }
}
