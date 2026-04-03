import {
  Component,
  EventEmitter,
  Inject,
  OnInit,
  Output,
  TemplateRef,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DIALOG_DATA } from '../services/dialog.service';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { SuspendNodeEntity } from '../../entities/definitions/suspend-node.entity';

@Component({
  selector: 'app-suspend-node-dialog',
  imports: [CommonModule, FormsModule, TabComponent, ContextViewerComponent],
  templateUrl: './suspend-node-dialog.component.html',
  styleUrls: ['./suspend-node-dialog.component.scss'],
})
export class SuspendNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: SuspendNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  constructor(
    @Inject(DIALOG_DATA)
    data: {
      node: SuspendNodeEntity;
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
