import { FormsModule } from '@angular/forms';
import { Component } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

@Component({
  selector: 'app-eval-start-run-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './eval-start-run-dialog.component.html',
})
export class EvalStartRunDialogComponent {
  public title = 'Start Evaluation Run';
  public message = '';
  public runName = '';
  public result = false;

  constructor(public bsModalRef: BsModalRef) {}

  startRun(): void {
    this.result = true;
    this.bsModalRef.hide();
  }

  cancel(): void {
    this.bsModalRef.hide();
  }
}
