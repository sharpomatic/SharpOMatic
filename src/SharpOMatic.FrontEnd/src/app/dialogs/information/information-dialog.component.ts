import { Component } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

@Component({
  selector: 'app-information-dialog',
  standalone: true,
  templateUrl: './information-dialog.component.html'
})
export class InformationDialogComponent {
  public title = '';
  public message = '';

  constructor(public bsModalRef: BsModalRef) {}

  ok(): void {
    this.bsModalRef.hide();
  }
}
