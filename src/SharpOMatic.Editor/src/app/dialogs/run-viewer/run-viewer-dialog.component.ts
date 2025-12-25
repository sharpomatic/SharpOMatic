import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Inject, OnInit, Output, TemplateRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { ContextEntryListEntity, ContextEntryListSnapshot } from '../../entities/definitions/context-entry-list.entity';
import { ContextEntryEntity } from '../../entities/definitions/context-entry.entity';
import { ContextEntryType } from '../../entities/enumerations/context-entry-type';
import { RunStatus } from '../../enumerations/run-status';
import { NodeStatus } from '../../enumerations/node-status';
import { getNodeSymbol } from '../../entities/enumerations/node-type';
import { RunProgressModel } from '../../pages/workflow/interfaces/run-progress-model';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { DIALOG_DATA } from '../services/dialog.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { MonacoService } from '../../services/monaco.service';

interface RunPropertyRow {
  label: string;
  value: string;
  multiline?: boolean;
  status?: boolean;
  date?: boolean;
}

@Component({
  selector: 'app-run-viewer-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorModule,
    TabComponent,
    ContextViewerComponent
  ],
  templateUrl: './run-viewer-dialog.component.html',
  styleUrls: ['./run-viewer-dialog.component.scss']
})
export class RunViewerDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('runTab', { static: true }) runTab!: TemplateRef<unknown>;
  @ViewChild('inputTab', { static: true }) inputTab!: TemplateRef<unknown>;
  @ViewChild('outputTab', { static: true }) outputTab!: TemplateRef<unknown>;
  @ViewChild('traceTab', { static: true }) traceTab!: TemplateRef<unknown>;

  public run: RunProgressModel;
  public tabs: TabItem[] = [];
  public activeTabId = 'run';
  public runProperties: RunPropertyRow[] = [];
  public runInputs = ContextEntryListEntity.fromSnapshot(ContextEntryListEntity.defaultSnapshot());
  public outputContexts: string[] = [];
  public traces: TraceProgressModel[] = [];
  public isLoadingTraces = true;
  public readonly RunStatus = RunStatus;
  public readonly NodeStatus = NodeStatus;
  public readonly contextEntryType = ContextEntryType;
  public readonly getNodeSymbol = getNodeSymbol;

  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly jsonViewerOptions = { ...MonacoService.editorOptionsJson, readOnly: true };
  private readonly csharpViewerOptions = { ...MonacoService.editorOptionsCSharp, readOnly: true };

  constructor(@Inject(DIALOG_DATA) data: { run: RunProgressModel }) {
    this.run = data.run;
    this.outputContexts = this.run.outputContext ? [this.run.outputContext] : [];
  }

  ngOnInit(): void {
    this.tabs = [
      { id: 'run', title: 'Run', content: this.runTab },
      { id: 'input', title: 'Input', content: this.inputTab },
      { id: 'output', title: 'Output', content: this.outputTab },
      { id: 'trace', title: 'Trace', content: this.traceTab }
    ];

    this.runInputs = this.loadInputEntries();
    this.runProperties = this.buildRunProperties();
    this.loadTraces();
  }

  onClose(): void {
    this.close.emit();
  }

  public getEntryTypeDisplay(type: ContextEntryType): string {
    switch (type) {
      case ContextEntryType.Expression:
        return '(expression)';
      case ContextEntryType.JSON:
        return '(json)';
      default:
        return ContextEntryType[type].toLowerCase();
    }
  }

  public getEditorOptions(entry: ContextEntryEntity): any {
    if (entry.entryType() === ContextEntryType.JSON) {
      return this.jsonViewerOptions;
    }

    return this.csharpViewerOptions;
  }

  private loadInputEntries(): ContextEntryListEntity {
    const rawEntries = this.run.inputEntries;
    if (!rawEntries) {
      return ContextEntryListEntity.fromSnapshot(ContextEntryListEntity.defaultSnapshot());
    }

    try {
      const parsed = JSON.parse(rawEntries);
      if (parsed && typeof parsed === 'object' && Array.isArray(parsed.entries)) {
        return ContextEntryListEntity.fromSnapshot(parsed as ContextEntryListSnapshot);
      }
    } catch {
      // Ignore invalid payloads.
    }

    return ContextEntryListEntity.fromSnapshot(ContextEntryListEntity.defaultSnapshot());
  }

  private loadTraces(): void {
    this.isLoadingTraces = true;
    this.serverRepository.getRunTraces(this.run.runId).subscribe(traces => {
      this.traces = traces ?? [];
      this.isLoadingTraces = false;
    });
  }

  private buildRunProperties(): RunPropertyRow[] {
    return [
      { label: 'Created', value: this.run.created ?? '', date: true },
      { label: 'Status', value: this.formatRunStatus(this.run.runStatus), status: true },
      { label: 'Duration', value: this.formatDuration(this.run.started, this.run.stopped) },
      { label: 'Started', value: this.run.started ?? '', date: true },
      { label: 'Stopped', value: this.run.stopped ?? '', date: true },
      { label: 'Message', value: this.formatValue(this.run.message) },
      { label: 'Error', value: this.formatValue(this.run.error), multiline: true }
    ];
  }

  private formatDuration(started?: string | null, stopped?: string | null): string {
    if (!started || !stopped) {
      return '-';
    }

    const startedMs = Date.parse(started);
    const stoppedMs = Date.parse(stopped);
    if (!Number.isFinite(startedMs) || !Number.isFinite(stoppedMs) || stoppedMs < startedMs) {
      return '-';
    }

    const totalSeconds = Math.floor((stoppedMs - startedMs) / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    return `${this.pad(hours)}:${this.pad(minutes)}:${this.pad(seconds)}`;
  }

  private formatRunStatus(status: RunStatus): string {
    return RunStatus[status] ?? status.toString();
  }

  private formatValue(value?: string | null): string {
    const trimmed = (value ?? '').trim();
    return trimmed.length ? trimmed : '';
  }

  private pad(value: number): string {
    return value.toString().padStart(2, '0');
  }
}
