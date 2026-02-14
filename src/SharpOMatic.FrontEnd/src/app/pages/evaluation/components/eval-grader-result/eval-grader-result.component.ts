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
}

