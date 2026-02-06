import { CommonModule, DatePipe } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ElementRef,
  ViewChild,
  inject,
} from '@angular/core';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { forkJoin } from 'rxjs';
import { AssetPreviewDialogComponent } from '../../dialogs/asset-preview/asset-preview-dialog.component';
import { AssetTextDialogComponent } from '../../dialogs/asset-text/asset-text-dialog.component';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { AssetScope } from '../../enumerations/asset-scope';
import { AssetSortField } from '../../enumerations/asset-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';
import { formatByteSize } from '../../helper/format-size';
import { ServerRepositoryService } from '../../services/server.repository.service';
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
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  private textModalRef: BsModalRef<AssetTextDialogComponent> | undefined;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;
  private readonly editableTextMediaTypes = new Set([
    'text/plain',
    'text/markdown',
    'text/csv',
    'text/html',
    'text/xml',
    'text/css',
    'text/javascript',
    'application/json',
    'application/xml',
    'application/x-yaml',
    'application/javascript',
  ]);

  public assets: AssetSummary[] = [];
  public assetsPage = 1;
  public assetsTotal = 0;
  public readonly assetsPageSize = 50;
  public isUploading = false;
  public readonly AssetSortField = AssetSortField;
  public readonly SortDirection = SortDirection;
  public assetsSortField: AssetSortField = AssetSortField.Name;
  public assetsSortDirection: SortDirection = SortDirection.Descending;
  public searchText = '';
  private searchDebounceId: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
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
    const uploads = files.map((file) =>
      this.serverRepository.uploadAsset(file, file.name, AssetScope.Library),
    );
    forkJoin(uploads).subscribe(() => {
      this.isUploading = false;
      this.refreshAssets();
      if (input) {
        input.value = '';
      }
    });
  }

  deleteAsset(asset: AssetSummary): void {
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Asset',
        message: `Are you sure you want to delete the asset '${asset.name}'?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (this.confirmModalRef?.content?.result) {
        this.serverRepository.deleteAsset(asset.assetId).subscribe(() => {
          this.refreshAssets();
        });
      }
    });
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
    const mediaType = this.normalizeMediaType(asset.mediaType);
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
    const mediaType = this.normalizeMediaType(asset.mediaType);
    return this.editableTextMediaTypes.has(mediaType);
  }

  private normalizeMediaType(mediaType: string | undefined | null): string {
    if (!mediaType) {
      return '';
    }

    return mediaType.split(';', 2)[0].trim().toLowerCase();
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

  clearSearch(): void {
    if (!this.searchText) {
      return;
    }

    this.searchText = '';
    this.applySearch();
  }

  private refreshAssets(): void {
    const search = this.searchText.trim();
    this.serverRepository
      .getAssetsCount(AssetScope.Library, search)
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
    this.serverRepository
      .getAssets(
        AssetScope.Library,
        skip,
        this.assetsPageSize,
        this.assetsSortField,
        this.assetsSortDirection,
        search,
      )
      .subscribe((assets) => {
        this.assets = assets;
        this.assetsPage = page;
      });
  }

  private scheduleSearch(): void {
    if (this.searchDebounceId) {
      clearTimeout(this.searchDebounceId);
    }

    this.searchDebounceId = setTimeout(() => this.applySearch(), 250);
  }
}
