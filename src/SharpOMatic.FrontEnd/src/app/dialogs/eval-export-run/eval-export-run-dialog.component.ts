import { FormsModule } from '@angular/forms';
import { Component, OnInit } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';
import { EvalRunCsvExportRequest } from '../../eval/definitions/eval-run-csv-export-request';
import { EvalRunExportField } from '../../eval/enumerations/eval-run-export-field';

export interface EvalExportColumnOption {
  evalColumnId: string;
  name: string;
}

@Component({
  selector: 'app-eval-export-run-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './eval-export-run-dialog.component.html',
})
export class EvalExportRunDialogComponent implements OnInit {
  public title = 'Export Run to CSV';
  public columns: EvalExportColumnOption[] = [];
  public readonly fieldOptions = [
    { value: EvalRunExportField.RowNumber, label: 'Row number' },
    { value: EvalRunExportField.Status, label: 'Status' },
    { value: EvalRunExportField.Score, label: 'Score' },
    { value: EvalRunExportField.Error, label: 'Error' },
    { value: EvalRunExportField.Started, label: 'Started' },
    { value: EvalRunExportField.Finished, label: 'Finished' },
  ];
  public selectedColumnIds = new Set<string>();
  public selectedFields = new Set<EvalRunExportField>(
    this.fieldOptions.map((option) => option.value),
  );
  public graderPathsText = '';
  public errorMessage: string | null = null;
  public result = false;

  constructor(public bsModalRef: BsModalRef) {}

  ngOnInit(): void {
    this.selectedColumnIds = new Set(
      this.columns.map((column) => column.evalColumnId),
    );
  }

  isColumnSelected(column: EvalExportColumnOption): boolean {
    return this.selectedColumnIds.has(column.evalColumnId);
  }

  onColumnToggle(column: EvalExportColumnOption, checked: boolean): void {
    this.toggle(this.selectedColumnIds, column.evalColumnId, checked);
  }

  isFieldSelected(field: EvalRunExportField): boolean {
    return this.selectedFields.has(field);
  }

  onFieldToggle(field: EvalRunExportField, checked: boolean): void {
    this.toggle(this.selectedFields, field, checked);
  }

  export(): void {
    const request = this.getRequest();
    if (
      request.columnIds.length === 0 &&
      request.fields.length === 0 &&
      request.graderPaths.length === 0
    ) {
      this.errorMessage = 'Select at least one column or enter a grader path.';
      return;
    }

    this.result = true;
    this.bsModalRef.hide();
  }

  cancel(): void {
    this.bsModalRef.hide();
  }

  getRequest(): EvalRunCsvExportRequest {
    return {
      columnIds: this.columns
        .filter((column) => this.selectedColumnIds.has(column.evalColumnId))
        .map((column) => column.evalColumnId),
      fields: this.fieldOptions
        .filter((option) => this.selectedFields.has(option.value))
        .map((option) => option.value),
      graderPaths: this.graderPathsText
        .split(/[\s,]+/)
        .filter((path) => path.length > 0),
    };
  }

  private toggle<T>(selection: Set<T>, value: T, checked: boolean): void {
    this.errorMessage = null;
    if (checked) {
      selection.add(value);
    } else {
      selection.delete(value);
    }
  }
}
