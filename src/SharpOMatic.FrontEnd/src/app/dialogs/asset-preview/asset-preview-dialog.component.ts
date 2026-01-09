import { Component } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

@Component({
  selector: 'app-asset-preview-dialog',
  standalone: true,
  templateUrl: './asset-preview-dialog.component.html',
  styleUrls: ['./asset-preview-dialog.component.scss'],
})
export class AssetPreviewDialogComponent {
  public title = '';
  public imageUrl = '';
  public altText = '';

  constructor(public bsModalRef: BsModalRef) {}

  close(): void {
    this.bsModalRef.hide();
  }
}
