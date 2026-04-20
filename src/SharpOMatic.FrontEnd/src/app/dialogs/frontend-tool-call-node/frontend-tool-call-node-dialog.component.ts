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
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { DIALOG_DATA } from '../services/dialog.service';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { FrontendToolCallNodeEntity } from '../../entities/definitions/frontend-tool-call-node.entity';
import { FrontendToolCallArgumentsMode } from '../../entities/enumerations/frontend-tool-call-arguments-mode';
import { FrontendToolCallChatPersistenceMode } from '../../entities/enumerations/frontend-tool-call-chat-persistence-mode';
import { MonacoService } from '../../services/monaco.service';

@Component({
  selector: 'app-frontend-tool-call-node-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorModule,
    TabComponent,
    ContextViewerComponent,
  ],
  templateUrl: './frontend-tool-call-node-dialog.component.html',
  styleUrls: ['./frontend-tool-call-node-dialog.component.scss'],
})
export class FrontendToolCallNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public readonly ArgumentsMode = FrontendToolCallArgumentsMode;
  public readonly ChatPersistenceMode = FrontendToolCallChatPersistenceMode;
  public node: FrontendToolCallNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public editorOptionsJson = MonacoService.editorOptionsJson;
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  constructor(
    @Inject(DIALOG_DATA)
    data: {
      node: FrontendToolCallNodeEntity;
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
