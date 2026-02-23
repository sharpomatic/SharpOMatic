import { Component, ElementRef, ViewChild } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

@Component({
  selector: 'app-text-input-dialog',
  standalone: true,
  templateUrl: './text-input-dialog.component.html',
})
export class TextInputDialogComponent {
  @ViewChild('valueInput') private valueInput?: ElementRef<HTMLInputElement>;

  public title = '';
  public label = '';
  public value = '';
  public placeholder = '';
  public confirmText = 'Save';
  public result: string | null = null;

  constructor(public bsModalRef: BsModalRef) {}

  ngAfterViewInit(): void {
    setTimeout(() => this.valueInput?.nativeElement.focus(), 0);
  }

  onValueChange(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    this.value = input?.value ?? '';
  }

  canConfirm(): boolean {
    return this.value.trim().length > 0;
  }

  confirm(): void {
    if (!this.canConfirm()) {
      return;
    }

    this.result = this.value.trim();
    this.bsModalRef.hide();
  }

  close(): void {
    this.bsModalRef.hide();
  }
}
