import {
  Component,
  EventEmitter,
  Inject,
  OnInit,
  Output,
  TemplateRef,
  ViewChild,
} from '@angular/core';
import { DIALOG_DATA } from '../services/dialog.service';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { FanInNodeEntity } from '../../entities/definitions/fan-in-node.entity';

@Component({
  selector: 'app-fan-in-node-dialog',
  imports: [CommonModule, FormsModule, TabComponent, ContextViewerComponent],
  templateUrl: './fan-in-node-dialog.component.html',
  styleUrls: ['./fan-in-node-dialog.component.scss'],
})
export class FanInNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: FanInNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  constructor(
    @Inject(DIALOG_DATA)
    data: {
      node: FanInNodeEntity;
      nodeTraces: TraceProgressModel[];
    },
  ) {
    this.node = data.node;
    this.inputTraces = (data.nodeTraces ?? [])
      .map((trace) => trace.inputContext)
      .filter((context): context is string => context != null);
    this.outputTraces = (data.nodeTraces ?? [])
      .map((trace) => trace.outputContext)
      .filter((context): context is string => context != null);
  }

  ngOnInit(): void {
    this.tabs = [
      { id: 'details', title: 'Details', content: this.detailsTab },
      { id: 'inputs', title: 'Inputs', content: this.inputsTab },
      { id: 'outputs', title: 'Outputs', content: this.outputsTab },
    ];
  }

  onClose(): void {
    this.close.emit();
  }
}
