import { CommonModule } from '@angular/common';
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
import { BsModalService } from 'ngx-bootstrap/modal';
import { FormsModule } from '@angular/forms';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import {
  ContextEntryListEntity,
  ContextEntryListSnapshot,
} from '../../entities/definitions/context-entry-list.entity';
import { ContextEntryEntity } from '../../entities/definitions/context-entry.entity';
import { ContextEntryType } from '../../entities/enumerations/context-entry-type';
import {
  parseAssetRefListValue,
  parseAssetRefValue,
} from '../../entities/definitions/asset-ref';
import { RunStatus } from '../../enumerations/run-status';
import { AssetScope } from '../../enumerations/asset-scope';
import { AssetSortField } from '../../enumerations/asset-sort-field';
import { SortDirection } from '../../enumerations/sort-direction';
import { RunProgressModel } from '../../pages/workflow/interfaces/run-progress-model';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { DIALOG_DATA } from '../services/dialog.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { MonacoService } from '../../services/monaco.service';
import { TraceViewerComponent } from '../../components/trace-viewer/trace-viewer.component';
import { AssetPreviewDialogComponent } from '../asset-preview/asset-preview-dialog.component';
import { AssetTextDialogComponent } from '../asset-text/asset-text-dialog.component';
import { AssetSummary } from '../../pages/assets/interfaces/asset-summary';
import { formatByteSize } from '../../helper/format-size';

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
    ContextViewerComponent,
    TraceViewerComponent,
  ],
  templateUrl: './run-viewer-dialog.component.html',
  styleUrls: ['./run-viewer-dialog.component.scss'],
  providers: [BsModalService],
})
export class RunViewerDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('runTab', { static: true }) runTab!: TemplateRef<unknown>;
  @ViewChild('inputTab', { static: true }) inputTab!: TemplateRef<unknown>;
  @ViewChild('outputTab', { static: true }) outputTab!: TemplateRef<unknown>;
  @ViewChild('traceTab', { static: true }) traceTab!: TemplateRef<unknown>;
  @ViewChild('assetsTab', { static: true }) assetsTab!: TemplateRef<unknown>;

  public run: RunProgressModel;
  public tabs: TabItem[] = [];
  public activeTabId = 'run';
  public runProperties: RunPropertyRow[] = [];
  public runInputs = ContextEntryListEntity.fromSnapshot(
    ContextEntryListEntity.defaultSnapshot(),
  );
  public outputContexts: string[] = [];
  public traces: TraceProgressModel[] = [];
  public isLoadingTraces = true;
  public runAssets: AssetSummary[] = [];
  public readonly RunStatus = RunStatus;
  public readonly contextEntryType = ContextEntryType;

  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly jsonViewerOptions = {
    ...MonacoService.editorOptionsJson,
    readOnly: true,
  };
  private readonly csharpViewerOptions = {
    ...MonacoService.editorOptionsCSharp,
    readOnly: true,
  };
  private readonly viewableTextMediaTypes = new Set([
    'text/plain',
    'text/markdown',
    'text/csv',
    'text/html',
    'text/xml',
    'text/css',
    'text/javascript',
    'application/json',
    'application/xml',
    'application/x-yaml',
    'application/javascript',
  ]);

  constructor(@Inject(DIALOG_DATA) data: { run: RunProgressModel }) {
    this.run = data.run;
    this.outputContexts = this.run.outputContext
      ? [this.run.outputContext]
      : [];
  }

  ngOnInit(): void {
    this.tabs = [
      { id: 'run', title: 'Run', content: this.runTab },
      { id: 'input', title: 'Input', content: this.inputTab },
      { id: 'output', title: 'Output', content: this.outputTab },
      { id: 'trace', title: 'Trace', content: this.traceTab },
      { id: 'assets', title: 'Assets', content: this.assetsTab },
    ];

    this.runInputs = this.loadInputEntries();
    this.runProperties = this.buildRunProperties();
    this.loadTraces();
    this.loadRunAssets();
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
      case ContextEntryType.AssetRef:
        return 'asset';
      case ContextEntryType.AssetRefList:
        return 'asset list';
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

  public getAssetEntryDisplay(entry: ContextEntryEntity): string {
    if (entry.entryType() === ContextEntryType.AssetRef) {
      return parseAssetRefValue(entry.entryValue())?.name ?? '';
    }

    if (entry.entryType() === ContextEntryType.AssetRefList) {
      return parseAssetRefListValue(entry.entryValue())
        .map((asset) => asset.name)
        .join(', ');
    }

    return entry.entryValue();
  }

  public formatSize(bytes: number): string {
    return formatByteSize(bytes);
  }

  public isImageAsset(asset: AssetSummary): boolean {
    const mediaType = this.normalizeMediaType(asset.mediaType);
    return mediaType.startsWith('image/');
  }

  public isViewableTextAsset(asset: AssetSummary): boolean {
    const mediaType = this.normalizeMediaType(asset.mediaType);
    return this.viewableTextMediaTypes.has(mediaType);
  }

  public openAssetPreview(asset: AssetSummary): void {
    if (!this.isImageAsset(asset)) {
      return;
    }

    this.modalService.show(AssetPreviewDialogComponent, {
      initialState: {
        assetId: asset.assetId,
        title: asset.name,
        fileName: asset.name,
        imageUrl: this.serverRepository.getAssetContentUrl(asset.assetId),
        altText: asset.name,
      },
      class: 'modal-fullscreen asset-preview-modal',
    });
  }

  public openAssetViewer(asset: AssetSummary): void {
    if (!this.isViewableTextAsset(asset)) {
      return;
    }

    this.modalService.show(AssetTextDialogComponent, {
      initialState: {
        assetId: asset.assetId,
        title: asset.name,
        readOnly: true,
      },
      class: 'modal-fullscreen asset-text-modal',
    });
  }

  private loadInputEntries(): ContextEntryListEntity {
    const rawEntries = this.run.inputEntries;
    if (!rawEntries) {
      return ContextEntryListEntity.fromSnapshot(
        ContextEntryListEntity.defaultSnapshot(),
      );
    }

    try {
      const parsed = JSON.parse(rawEntries);
      if (
        parsed &&
        typeof parsed === 'object' &&
        Array.isArray(parsed.entries)
      ) {
        return ContextEntryListEntity.fromSnapshot(
          parsed as ContextEntryListSnapshot,
        );
      }
    } catch {
      // Ignore invalid payloads.
    }

    return ContextEntryListEntity.fromSnapshot(
      ContextEntryListEntity.defaultSnapshot(),
    );
  }

  private loadTraces(): void {
    this.isLoadingTraces = true;
    this.serverRepository.getRunTraces(this.run.runId).subscribe((traces) => {
      this.traces = traces ?? [];
      this.isLoadingTraces = false;
    });
  }

  private loadRunAssets(): void {
    if (!this.run.runId) {
      this.runAssets = [];
      return;
    }

    this.serverRepository
      .getAssets(
        AssetScope.Run,
        0,
        0,
        AssetSortField.Created,
        SortDirection.Descending,
        '',
        this.run.runId,
      )
      .subscribe((assets) => {
        this.runAssets = assets ?? [];
      });
  }

  private buildRunProperties(): RunPropertyRow[] {
    return [
      { label: 'Created', value: this.run.created ?? '', date: true },
      {
        label: 'Status',
        value: this.formatRunStatus(this.run.runStatus),
        status: true,
      },
      {
        label: 'Duration',
        value: this.formatDuration(this.run.started, this.run.stopped),
      },
      { label: 'Started', value: this.run.started ?? '', date: true },
      { label: 'Stopped', value: this.run.stopped ?? '', date: true },
      { label: 'Message', value: this.formatValue(this.run.message) },
      {
        label: 'Error',
        value: this.formatValue(this.run.error),
        multiline: true,
      },
    ];
  }

  private formatDuration(
    started?: string | null,
    stopped?: string | null,
  ): string {
    if (!started || !stopped) {
      return '';
    }

    const startedMs = Date.parse(started);
    const stoppedMs = Date.parse(stopped);
    if (
      !Number.isFinite(startedMs) ||
      !Number.isFinite(stoppedMs) ||
      stoppedMs < startedMs
    ) {
      return '';
    }

    const durationMs = stoppedMs - startedMs;
    if (durationMs <= 0) {
      return '';
    }

    const roundedMs = Math.ceil(durationMs / 10) * 10;
    const totalSeconds = Math.floor(roundedMs / 1000);
    const hundredths = Math.floor((roundedMs % 1000) / 10);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    return `${this.pad(hours)}:${this.pad(minutes)}:${this.pad(seconds)}.${this.pad(hundredths)}`;
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

  private normalizeMediaType(mediaType: string | undefined | null): string {
    if (!mediaType) {
      return '';
    }

    return mediaType.split(';', 2)[0].trim().toLowerCase();
  }
}
