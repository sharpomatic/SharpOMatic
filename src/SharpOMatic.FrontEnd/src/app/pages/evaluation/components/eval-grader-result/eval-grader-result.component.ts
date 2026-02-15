import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { EvalRunGraderSummaryDetailSnapshot } from '../../../../eval/definitions/eval-run-detail';

@Component({
  selector: 'app-eval-grader-result',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './eval-grader-result.component.html',
  styleUrls: ['./eval-grader-result.component.scss'],
})
export class EvalGraderResultComponent {
  @Input({ required: true }) summary!: EvalRunGraderSummaryDetailSnapshot;

  formatMetric(value: number | null): string {
    if (value === null || value === undefined) {
      return '-';
    }

    return value.toFixed(3);
  }

  formatPercent(value: number | null): string {
    if (value === null || value === undefined) {
      return '-';
    }

    return `${(value * 100).toFixed(1)}%`;
  }

  getPassRateDisplay(summary: EvalRunGraderSummaryDetailSnapshot): string {
    return this.formatPercent(summary.passRate);
  }

  getPassRatePercent(summary: EvalRunGraderSummaryDetailSnapshot): number {
    if (summary.passRate === null || summary.passRate === undefined) {
      return 0;
    }

    return this.clampPercent(summary.passRate * 100);
  }

  getPassRateRemainderPercent(summary: EvalRunGraderSummaryDetailSnapshot): number {
    return 100 - this.getPassRatePercent(summary);
  }

  getCompletedPercent(summary: EvalRunGraderSummaryDetailSnapshot): number {
    if (!summary.totalCount || summary.totalCount <= 0) {
      return 0;
    }

    return this.clampPercent((summary.completedCount / summary.totalCount) * 100);
  }

  private clampPercent(value: number): number {
    const safeValue = Number.isFinite(value) ? value : 0;
    return Math.min(100, Math.max(0, safeValue));
  }
}
