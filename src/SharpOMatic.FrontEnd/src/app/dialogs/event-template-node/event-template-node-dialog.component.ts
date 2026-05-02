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
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { EventTemplateNodeEntity } from '../../entities/definitions/event-template-node.entity';
import { EventTemplateOutputMode } from '../../entities/enumerations/event-template-output-mode';
import { StreamMessageRole } from '../../enumerations/stream-message-role';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { DIALOG_DATA } from '../services/dialog.service';

@Component({
  selector: 'app-event-template-node-dialog',
  imports: [CommonModule, FormsModule, TabComponent, ContextViewerComponent],
  templateUrl: './event-template-node-dialog.component.html',
  styleUrls: ['./event-template-node-dialog.component.scss'],
})
export class EventTemplateNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: EventTemplateNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';
  public readonly EventTemplateOutputMode = EventTemplateOutputMode;
  public readonly outputModeOptions = [
    { value: EventTemplateOutputMode.Text, label: 'Text' },
    { value: EventTemplateOutputMode.Reasoning, label: 'Reasoning' },
  ];
  public readonly textRoleOptions = [
    { value: StreamMessageRole.Assistant, label: 'Assistant' },
    { value: StreamMessageRole.User, label: 'User' },
    { value: StreamMessageRole.System, label: 'System' },
    { value: StreamMessageRole.Developer, label: 'Developer' },
    { value: StreamMessageRole.Tool, label: 'Tool' },
  ];

  constructor(
    @Inject(DIALOG_DATA)
    data: {
      node: EventTemplateNodeEntity;
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
