import { Component } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  templateUrl: './confirm-dialog.component.html',
})
export class ConfirmDialogComponent {
  public title = '';
  public message = '';
  public result = false;

  constructor(public bsModalRef: BsModalRef) {}

  confirm(): void {
    this.result = true;
    this.bsModalRef.hide();
  }

  decline(): void {
    this.bsModalRef.hide();
  }
}
