import { FormsModule } from '@angular/forms';
import { Component, OnInit } from '@angular/core';
import { BsModalRef } from 'ngx-bootstrap/modal';

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
  public enableSampling = false;
  public sampleCount = 1;
  public maxSampleCount = 0;
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

    if (!this.canUseSampling()) {
      this.enableSampling = false;
    }
  }

  startRun(): void {
    this.sampleCount = this.clampSampleCount(this.sampleCount);
    if (!this.canUseSampling()) {
      this.enableSampling = false;
    }

    this.result = true;
    this.bsModalRef.hide();
  }

  cancel(): void {
    this.bsModalRef.hide();
  }

  onEnableSamplingChange(value: boolean): void {
    this.enableSampling = value && this.canUseSampling();
    this.sampleCount = this.clampSampleCount(this.sampleCount);
  }

  onSampleCountChange(value: string | number): void {
    const parsed = typeof value === 'number' ? value : Number(value);
    this.sampleCount = this.clampSampleCount(parsed);
  }

  canUseSampling(): boolean {
    return this.maxSampleCount > 0;
  }

  getSelectedSampleCount(): number | null {
    if (!this.enableSampling || !this.canUseSampling()) {
      return null;
    }

    return this.clampSampleCount(this.sampleCount);
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
