import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { BsDropdownModule } from 'ngx-bootstrap/dropdown';
import { ModelPickerOption } from './model-picker-option';

let nextPickerId = 0;

@Component({
  selector: 'app-model-picker',
  standalone: true,
  imports: [CommonModule, BsDropdownModule],
  templateUrl: './model-picker.component.html',
  styleUrls: ['./model-picker.component.scss'],
})
export class ModelPickerComponent {
  @Input() options: ModelPickerOption[] = [];
  @Input() selectedId: string | null = null;
  @Input() disabled = false;
  @Input() controlId: string | null = null;
  @Input() placeholder = 'Select model';
  @Input() allowEmptySelection = false;
  @Input() emptyOptionLabel = '(None)';
  @Output() selectionChange = new EventEmitter<string | null>();

  public readonly pickerId = `model-picker-${nextPickerId++}`;

  public get selectedOption(): ModelPickerOption | null {
    return this.options.find((option) => option.id === this.selectedId) ?? null;
  }

  public get triggerDisabled(): boolean {
    return this.disabled || (!this.options.length && !this.allowEmptySelection);
  }

  public get resolvedControlId(): string {
    return this.controlId ?? this.pickerId;
  }

  public selectOption(id: string | null): void {
    if (this.triggerDisabled) {
      return;
    }

    this.selectionChange.emit(id);
  }
}
