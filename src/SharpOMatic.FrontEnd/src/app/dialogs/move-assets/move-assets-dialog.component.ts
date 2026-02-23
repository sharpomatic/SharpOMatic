import { Component } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

export const MoveAssetsTopLevelOptionValue = '__top-level__';

export interface MoveAssetFolderOption {
  folderId: string;
  name: string;
}

@Component({
  selector: 'app-move-assets-dialog',
  standalone: true,
  templateUrl: './move-assets-dialog.component.html',
})
export class MoveAssetsDialogComponent {
  public readonly topLevelOptionValue = MoveAssetsTopLevelOptionValue;
  public folders: MoveAssetFolderOption[] = [];
  public selectedFolderId: string | null = null;
  public result: string | undefined = undefined;

  constructor(public bsModalRef: BsModalRef) {}

  folderSelectionValue(): string {
    return this.selectedFolderId ?? MoveAssetsTopLevelOptionValue;
  }

  onFolderSelectionChange(event: Event): void {
    const input = event.target as HTMLSelectElement | null;
    const value = input?.value ?? '';
    this.selectedFolderId =
      value && value !== MoveAssetsTopLevelOptionValue ? value : null;
  }

  confirm(): void {
    this.result = this.selectedFolderId ?? MoveAssetsTopLevelOptionValue;
    this.bsModalRef.hide();
  }

  close(): void {
    this.bsModalRef.hide();
  }
}
