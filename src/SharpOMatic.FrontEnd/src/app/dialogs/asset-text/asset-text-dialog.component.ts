import { Component, OnInit, inject } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';
import { ServerRepositoryService } from '../../services/server.repository.service';

@Component({
  selector: 'app-asset-text-dialog',
  standalone: true,
  templateUrl: './asset-text-dialog.component.html',
  styleUrls: ['./asset-text-dialog.component.scss'],
})
export class AssetTextDialogComponent implements OnInit {
  private readonly serverRepository = inject(ServerRepositoryService);

  public assetId = '';
  public title = '';
  public content = '';
  public readOnly = false;
  public saved = false;
  public isLoading = true;
  public isSaving = false;
  private originalContent = '';

  constructor(public bsModalRef: BsModalRef) {}

  ngOnInit(): void {
    if (!this.assetId) {
      this.isLoading = false;
      return;
    }

    this.serverRepository.getAssetText(this.assetId).subscribe((result) => {
      this.content = result?.content ?? '';
      this.originalContent = this.content;
      this.isLoading = false;
    });
  }

  onContentChange(event: Event): void {
    if (this.readOnly) {
      return;
    }

    const input = event.target as HTMLTextAreaElement | null;
    this.content = input?.value ?? '';
  }

  canSave(): boolean {
    return (
      !this.readOnly &&
      !this.isLoading &&
      !this.isSaving &&
      this.content !== this.originalContent
    );
  }

  save(): void {
    if (!this.canSave() || !this.assetId) {
      return;
    }

    this.isSaving = true;
    this.serverRepository
      .updateAssetText(this.assetId, this.content)
      .subscribe((success) => {
        if (success) {
          this.originalContent = this.content;
          this.saved = true;
        }

        this.isSaving = false;
      });
  }

  download(): void {
    if (!this.assetId || this.isLoading) {
      return;
    }

    this.serverRepository.getAssetContent(this.assetId).subscribe((blob) => {
      if (!blob) {
        return;
      }

      const fileName = this.title || 'asset';
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
