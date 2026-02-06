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
import { CodeNodeEntity } from '../../entities/definitions/code-node.entity';
import { CommonModule } from '@angular/common';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { MonacoService } from '../../services/monaco.service';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';

@Component({
  selector: 'app-code-node-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorModule,
    TabComponent,
    ContextViewerComponent,
  ],
  templateUrl: './code-node-dialog.component.html',
  styleUrls: ['./code-node-dialog.component.scss'],
})
export class CodeNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: CodeNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public editorOptions = MonacoService.editorOptionsCSharp;
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  constructor(
    @Inject(DIALOG_DATA)
    data: {
      node: CodeNodeEntity;
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
