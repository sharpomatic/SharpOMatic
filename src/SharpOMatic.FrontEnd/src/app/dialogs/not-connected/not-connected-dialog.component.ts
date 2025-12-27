import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BsModalRef } from 'ngx-bootstrap/modal';
import { API_URL } from '../../components/app/app.tokens';

@Component({
  selector: 'app-not-connected-dialog',
  templateUrl: './not-connected-dialog.component.html',
  styleUrls: ['./not-connected-dialog.component.scss'],
  standalone: true,
  imports: [CommonModule],
})
export class NotConnectedDialogComponent {
  public readonly apiUrl = inject(API_URL);

  constructor(public bsModalRef: BsModalRef) {}
}
