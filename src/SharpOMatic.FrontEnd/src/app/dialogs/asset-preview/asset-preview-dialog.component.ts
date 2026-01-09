import { Component, inject } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';
import { ServerRepositoryService } from '../../services/server.repository.service';

@Component({
  selector: 'app-asset-preview-dialog',
  standalone: true,
  templateUrl: './asset-preview-dialog.component.html',
  styleUrls: ['./asset-preview-dialog.component.scss'],
})
export class AssetPreviewDialogComponent {
  private readonly serverRepository = inject(ServerRepositoryService);

  public assetId = '';
  public title = '';
  public fileName = '';
  public imageUrl = '';
  public altText = '';

  constructor(public bsModalRef: BsModalRef) {}

  download(): void {
    if (!this.assetId) {
      return;
    }

    this.serverRepository.getAssetContent(this.assetId).subscribe((blob) => {
      if (!blob) {
        return;
      }

      const fileName = this.fileName || this.title || 'asset';
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.style.display = 'none';
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    });
  }

  close(): void {
    this.bsModalRef.hide();
  }
}
