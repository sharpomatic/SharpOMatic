import { Component, EventEmitter, Inject, OnInit, Output, TemplateRef, ViewChild } from '@angular/core';
import { DIALOG_DATA } from '../services/dialog.service';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { BatchNodeEntity } from '../../entities/definitions/batch-node.entity';

@Component({
  selector: 'app-batch-node-dialog',
  imports: [
    CommonModule,
    FormsModule,
    TabComponent,
    ContextViewerComponent
  ],
  templateUrl: './batch-node-dialog.component.html',
  styleUrls: ['./batch-node-dialog.component.scss']
})
export class BatchNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: BatchNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  constructor(@Inject(DIALOG_DATA) data: { node: BatchNodeEntity, nodeTraces: TraceProgressModel[] }) {
    this.node = data.node;
    this.inputTraces = (data.nodeTraces ?? []).map(trace => trace.inputContext).filter((context): context is string => context != null);
    this.outputTraces = (data.nodeTraces ?? []).map(trace => trace.outputContext).filter((context): context is string => context != null);
  }

  ngOnInit(): void {
    this.tabs = [
      { id: 'details', title: 'Details', content: this.detailsTab },
      { id: 'inputs', title: 'Inputs', content: this.inputsTab },
      { id: 'outputs', title: 'Outputs', content: this.outputsTab }
    ];
  }

  onBatchSizeChange(value: string | number): void {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      this.node.batchSize.set(parsed);
    }
  }

  onParallelBatchesChange(value: string | number): void {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      this.node.parallelBatches.set(parsed);
    }
  }

  onClose(): void {
    this.close.emit();
  }
}
