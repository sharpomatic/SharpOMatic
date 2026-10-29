import { FormsModule } from '@angular/forms';
import { Component, OnInit } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

export type EvalRunSelectionMode = 'all' | 'sample' | 'rows';

@Component({
  selector: 'app-eval-start-run-dialog',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './eval-start-run-dialog.component.html',
})
export class EvalStartRunDialogComponent implements OnInit {
  public title = 'Start Evaluation Run';
  public message = '';
  public runName = '';
  public rowCount = 0;
  public rowIds: string[] = [];
  public selectionMode: EvalRunSelectionMode = 'all';
  public sampleCount = 1;
  public maxSampleCount = 0;
  public rowNumbersText = '';
  public rowNumbersError: string | null = null;
  public result = false;

  constructor(public bsModalRef: BsModalRef) {}
  ngOnInit(): void {
    const normalizedRowCount = Number(this.rowCount);
    this.maxSampleCount =
      Number.isFinite(normalizedRowCount) && normalizedRowCount > 0
        ? Math.trunc(normalizedRowCount)
        : 0;

    this.sampleCount =
      this.maxSampleCount > 0 ? Math.min(3, this.maxSampleCount) : 1;
  }

  startRun(): void {
    this.sampleCount = this.clampSampleCount(this.sampleCount);

    if (this.selectionMode === 'sample' && !this.canUseSampling()) {
      this.selectionMode = 'all';
    }

    if (this.selectionMode === 'rows') {
      this.rowNumbersError = this.validateRowNumbers();
      if (this.rowNumbersError) {
        return;
      }
    }

    this.result = true;
    this.bsModalRef.hide();
  }

  cancel(): void {
    this.bsModalRef.hide();
  }

  onSelectionModeChange(value: EvalRunSelectionMode): void {
    this.selectionMode = value;
    this.rowNumbersError = null;
    this.sampleCount = this.clampSampleCount(this.sampleCount);
  }

  onRowNumbersChange(value: string): void {
    this.rowNumbersText = value;
    this.rowNumbersError = null;
  }

  onSampleCountChange(value: string | number): void {
    const parsed = typeof value === 'number' ? value : Number(value);
    this.sampleCount = this.clampSampleCount(parsed);
  }

  canUseSampling(): boolean {
    return this.maxSampleCount > 0;
  }

  canUseSpecificRows(): boolean {
    return this.rowIds.length > 0;
  }

  getSelectedSampleCount(): number | null {
    if (this.selectionMode !== 'sample' || !this.canUseSampling()) {
      return null;
    }

    return this.clampSampleCount(this.sampleCount);
  }

  getSelectedEvalRowIds(): string[] | null {
    if (this.selectionMode !== 'rows') {
      return null;
    }

    const rowNumbers = this.parseRowNumbers();
    if (!rowNumbers) {
      return null;
    }

    return rowNumbers.map((rowNumber) => this.rowIds[rowNumber - 1]);
  }

  private validateRowNumbers(): string | null {
    const tokens = this.rowNumbersText
      .split(/[\s,]+/)
      .filter((token) => token.length > 0);

    if (tokens.length === 0) {
      return 'Enter at least one row number.';
    }

    for (const token of tokens) {
      if (!/^\d+$/.test(token)) {
        return `'${token}' is not a valid row number.`;
      }

      const rowNumber = Number(token);
      if (rowNumber < 1 || rowNumber > this.rowIds.length) {
        return `Row ${token} does not exist. Row numbers are between 1 and ${this.rowIds.length}.`;
      }
    }

    return null;
  }

  private parseRowNumbers(): number[] | null {
    if (this.validateRowNumbers()) {
      return null;
    }

    const rowNumbers = this.rowNumbersText
      .split(/[\s,]+/)
      .filter((token) => token.length > 0)
      .map((token) => Number(token));

    return Array.from(new Set(rowNumbers));
  }

  private clampSampleCount(value: string | number): number {
    if (!this.canUseSampling()) {
      return 1;
    }

    const numeric = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(numeric)) {
      return 1;
    }

    return Math.max(1, Math.min(this.maxSampleCount, Math.trunc(numeric)));
  }
}
