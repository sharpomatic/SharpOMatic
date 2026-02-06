import {
  Component,
  EventEmitter,
  Inject,
  OnInit,
  Output,
  TemplateRef,
  ViewChild,
  inject,
} from '@angular/core';
import { DIALOG_DATA } from '../services/dialog.service';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { FanOutNodeEntity } from '../../entities/definitions/fan-out-node.entity';
import { DesignerUpdateService } from '../../components/designer/services/designer-update.service';
import { WorkflowService } from '../../pages/workflow/services/workflow.service';

@Component({
  selector: 'app-fan-out-node-dialog',
  imports: [CommonModule, FormsModule, TabComponent, ContextViewerComponent],
  templateUrl: './fan-out-node-dialog.component.html',
  styleUrls: ['./fan-out-node-dialog.component.scss'],
})
export class FanOutNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: FanOutNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  private readonly designerUpdateService = inject(DesignerUpdateService);
  private readonly workflowService = inject(WorkflowService);

  constructor(
    @Inject(DIALOG_DATA)
    data: {
      node: FanOutNodeEntity;
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

  onAddOutput(): void {
    this.node.addOutput('');
  }

  onDeleteOutput(index: number): void {
    this.designerUpdateService.deleteFanOutOutput(
      this.workflowService.workflow(),
      this.node,
      index,
    );
  }

  onUpdateOutput(index: number, name: string): void {
    this.node.updateOutput(index, name);
  }

  onClose(): void {
    this.close.emit();
  }
}
