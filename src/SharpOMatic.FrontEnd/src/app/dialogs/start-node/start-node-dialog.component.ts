import { Component, EventEmitter, Inject, OnInit, Output, TemplateRef, ViewChild } from '@angular/core';
import { DIALOG_DATA } from '../services/dialog.service';
import { FormsModule } from '@angular/forms';
import { StartNodeEntity } from '../../entities/definitions/start-node.entity';
import { CommonModule } from '@angular/common';
import { ContextEntryEntity } from '../../entities/definitions/context-entry.entity';
import { ContextEntryPurpose } from '../../entities/enumerations/context-entry-purpose';
import { ContextEntryType } from '../../entities/enumerations/context-entry-type';
import { MonacoService } from '../../services/monaco.service';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';

@Component({
  selector: 'app-start-node-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorModule,
    TabComponent,
    ContextViewerComponent
  ],
  templateUrl: './start-node-dialog.component.html',
  styleUrls: ['./start-node-dialog.component.scss']
})
export class StartNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: StartNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public contextEntryType = ContextEntryType;
  public contextEntryPurpose = ContextEntryPurpose;
  public contextEntryTypeKeys: string[] = [];
  public tabs: TabItem[] = [];
  public activeTabId: string = 'details';

  constructor(@Inject(DIALOG_DATA) data: { node: StartNodeEntity, nodeTraces: TraceProgressModel[] }) {
    this.node = data.node;
    this.inputTraces = (data.nodeTraces ?? []).map(trace => trace.inputContext).filter((context): context is string => context != null);
    this.outputTraces = (data.nodeTraces ?? []).map(trace => trace.outputContext).filter((context): context is string => context != null);
    this.contextEntryTypeKeys = Object.keys(this.contextEntryType).filter(k => isNaN(Number(k)));
  }

  ngOnInit(): void {
    this.tabs = [
      { id: 'details', title: 'Details', content: this.detailsTab },
      { id: 'inputs', title: 'Inputs', content: this.inputsTab },
      { id: 'outputs', title: 'Outputs', content: this.outputsTab }
    ];
  }

  getEnumValue(key: string): ContextEntryType {
    return this.contextEntryType[key as keyof typeof ContextEntryType];
  }

  getEnumDisplay(key: string): string {
    switch (key) {
      case 'Expression':
        return '(expression)';
      case 'JSON':
        return '(json)';  
      default:
        return key.toLowerCase();      
    }
  }    

  getEditorOptions(entry: ContextEntryEntity): any {
    if (entry.entryType() === ContextEntryType.JSON) {
      return MonacoService.editorOptionsJson;
    } else {
      return MonacoService.editorOptionsCSharp;
    }
  }

  onAppendEntry(): void {
    this.node.initializing().appendEntry({ purpose: ContextEntryPurpose.Input });
  }    

  onDeleteEntry(entryId: string): void {
    this.node.initializing().deleteEntry(entryId);
  }  

  canMoveEntryUp(entry: ContextEntryEntity): boolean {
    return this.hasSiblingEntry(entry, -1);
  }

  canMoveEntryDown(entry: ContextEntryEntity): boolean {
    return this.hasSiblingEntry(entry, 1);
  }

  onMoveEntryUp(entry: ContextEntryEntity): void {
    this.node.initializing().moveEntry(entry.id, 'up', entry.purpose());
  }

  onMoveEntryDown(entry: ContextEntryEntity): void {
    this.node.initializing().moveEntry(entry.id, 'down', entry.purpose());
  }

  onClose(): void {
    this.close.emit();
  }

  private hasSiblingEntry(entry: ContextEntryEntity, step: number): boolean {
    const entries = this.node.initializing().entries();
    const index = entries.findIndex(e => e.id === entry.id);
    if (index === -1) {
      return false;
    }

    let targetIndex = index + step;
    while (targetIndex >= 0 && targetIndex < entries.length) {
      if (entries[targetIndex].purpose() === entry.purpose()) {
        return true;
      }

      targetIndex += step;
    }

    return false;
  }    
}
