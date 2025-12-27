import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { getNodeSymbol } from '../../entities/enumerations/node-type';
import { NodeStatus } from '../../enumerations/node-status';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';

@Component({
  selector: 'app-trace-viewer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './trace-viewer.component.html',
  styleUrls: ['./trace-viewer.component.scss']
})
export class TraceViewerComponent {
  @Input() traces: TraceProgressModel[] = [];

  public readonly NodeStatus = NodeStatus;
  public readonly getNodeSymbol = getNodeSymbol;
}
