import { Component } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

export type UnsavedChangesDecision = 'save' | 'discard' | 'stay';

@Component({
  selector: 'app-unsaved-changes-dialog',
  standalone: true,
  templateUrl: './unsaved-changes-dialog.component.html'
})
export class UnsavedChangesDialogComponent {
  public title = 'Unsaved changes';
  public message = 'You have unsaved changes. What would you like to do?';
  public result: UnsavedChangesDecision = 'stay';

  constructor(public bsModalRef: BsModalRef) {}

  stay(): void {
    this.result = 'stay';
    this.bsModalRef.hide();
  }

  discard(): void {
    this.result = 'discard';
    this.bsModalRef.hide();
  }

  saveAndLeave(): void {
    this.result = 'save';
    this.bsModalRef.hide();
  }
}
