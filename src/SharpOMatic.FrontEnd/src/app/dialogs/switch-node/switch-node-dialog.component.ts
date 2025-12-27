import { Component, EventEmitter, Inject, OnInit, Output, TemplateRef, ViewChild, inject } from '@angular/core';
import { DIALOG_DATA } from '../services/dialog.service';
import { FormsModule } from '@angular/forms';
import { SwitchNodeEntity } from '../../entities/definitions/switch-node.entity';
import { CommonModule } from '@angular/common';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { MonacoService } from '../../services/monaco.service';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { DesignerUpdateService } from '../../components/designer/services/designer-update.service';
import { WorkflowService } from '../../pages/workflow/services/workflow.service';

@Component({
  selector: 'app-end-node-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorModule,
    TabComponent,
    ContextViewerComponent
  ],
  templateUrl: './switch-node-dialog.component.html',
  styleUrls: ['./switch-node-dialog.component.scss']
})
export class SwitchNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: SwitchNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public editorOptions = MonacoService.editorOptionsCSharp;
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  private readonly designerUpdateService = inject(DesignerUpdateService);
  private readonly workflowService = inject(WorkflowService);

  constructor(@Inject(DIALOG_DATA) data: { node: SwitchNodeEntity, nodeTraces: TraceProgressModel[] }) {
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

  onAddSwitch(): void {
    this.node.addSwitch();
  }

  onDeleteSwitch(entryId: string): void {
    this.designerUpdateService.deleteSwitch(this.workflowService.workflow(), this.node, entryId);
  }

  onUpSwitch(entryId: string): void {
    this.node.upSwitch(entryId);
  }

  onDownSwitch(entryId: string): void {
    this.node.downSwitch(entryId);
  }

  onClose(): void {
    this.close.emit();
  }
}
