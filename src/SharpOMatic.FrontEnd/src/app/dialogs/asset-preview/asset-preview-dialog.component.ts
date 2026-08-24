import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';
import { ServerRepositoryService } from '../../services/server.repository.service';

@Component({
  selector: 'app-asset-preview-dialog',
  standalone: true,
  templateUrl: './asset-preview-dialog.component.html',
  styleUrls: ['./asset-preview-dialog.component.scss'],
})
export class AssetPreviewDialogComponent implements OnInit, OnDestroy {
  private readonly serverRepository = inject(ServerRepositoryService);

  public assetId = '';
  public title = '';
  public fileName = '';
  public altText = '';
  public imageUrl = '';
  public isLoading = true;
  public loadFailed = false;

  private content: Blob | null = null;

  constructor(public bsModalRef: BsModalRef) {}

  ngOnInit(): void {
    if (!this.assetId) {
      this.isLoading = false;
      this.loadFailed = true;
      return;
    }

    // The content is fetched through the repository service rather than pointed at from an img src, because only
    // requests made through HttpClient pass the auth interceptor that attaches the bearer token. A browser initiated
    // img request carries no token and is rejected by hosts that require authentication.
    this.serverRepository.getAssetContent(this.assetId).subscribe((blob) => {
      this.isLoading = false;

      if (!blob) {
        this.loadFailed = true;
        return;
      }

      this.content = blob;
      this.imageUrl = window.URL.createObjectURL(blob);
    });
  }

  ngOnDestroy(): void {
    this.releaseImageUrl();
  }

  download(): void {
    if (!this.content) {
      return;
    }

    const fileName = this.fileName || this.title || 'asset';
    const url = window.URL.createObjectURL(this.content);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  }

  close(): void {
    this.bsModalRef.hide();
  }

  private releaseImageUrl(): void {
    if (!this.imageUrl) {
      return;
    }

    window.URL.revokeObjectURL(this.imageUrl);
    this.imageUrl = '';
  }
}
