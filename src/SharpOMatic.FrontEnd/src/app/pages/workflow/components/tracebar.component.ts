import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, TemplateRef, ViewChild, computed, inject } from '@angular/core';
import { RunStatus } from '../../../enumerations/run-status';
import { WorkflowService } from '../services/workflow.service';
import { ContextEntryType } from '../../../entities/enumerations/context-entry-type';
import { FormsModule } from '@angular/forms';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { ContextEntryEntity } from '../../../entities/definitions/context-entry.entity';
import { AssetRef, buildAssetRefListValue, buildAssetRefValue, parseAssetRefListValue, parseAssetRefValue } from '../../../entities/definitions/asset-ref';
import { MonacoService } from '../../../services/monaco.service';
import { TabComponent, TabItem } from '../../../components/tab/tab.component';
import { ContextViewerComponent } from '../../../components/context-viewer/context-viewer.component';
import { TraceViewerComponent } from '../../../components/trace-viewer/trace-viewer.component';
import { DialogService } from '../../../dialogs/services/dialog.service';
import { AssetPickerDialogComponent } from '../../../dialogs/asset-picker/asset-picker-dialog.component';
import { BsModalService } from 'ngx-bootstrap/modal';
import { AssetPreviewDialogComponent } from '../../../dialogs/asset-preview/asset-preview-dialog.component';
import { AssetTextDialogComponent } from '../../../dialogs/asset-text/asset-text-dialog.component';
import { AssetSummary } from '../../assets/interfaces/asset-summary';
import { formatByteSize } from '../../../helper/format-size';
import { ServerRepositoryService } from '../../../services/server.repository.service';

@Component({
  selector: 'app-tracebar',
  standalone: true,
  imports: [CommonModule, FormsModule, MonacoEditorModule, TabComponent, ContextViewerComponent, TraceViewerComponent],
  templateUrl: './tracebar.component.html',
  styleUrl: './tracebar.component.scss',
  providers: [BsModalService]
})
export class TracebarComponent implements OnInit, OnDestroy {
  @ViewChild('inputTab', { static: true }) inputTab!: TemplateRef<unknown>;
  @ViewChild('outputTab', { static: true }) outputTab!: TemplateRef<unknown>;
  @ViewChild('traceTab', { static: true }) traceTab!: TemplateRef<unknown>;
  @ViewChild('assetsTab', { static: true }) assetsTab!: TemplateRef<unknown>;
  @Output() public tracebarWidthChange = new EventEmitter<number>();

  public readonly workflowService = inject(WorkflowService);
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly dialogService = inject(DialogService);
  private readonly modalService = inject(BsModalService);
  public readonly contextEntryType = ContextEntryType;
  public readonly contextEntryTypeKeys = Object.keys(ContextEntryType).filter(k => isNaN(Number(k)));
  public readonly RunStatus = RunStatus;
  public isResizing = false;
  public tabs: TabItem[] = [];
  @Input() public activeTabId = 'input';
  @Output() public activeTabIdChange = new EventEmitter<string>();
  public readonly outputContexts = computed(() => {
    const output = this.workflowService.runProgress()?.outputContext;
    return output ? [output] : [];
  });

  private minWidth = 500;
  private maxWidth = 1200;
  private tracebarWidth = 800;
  private startX = 0;
  private startWidth = this.tracebarWidth;
  private readonly storageKey = 'tracebarWidth';

  private readonly onMouseMove = (event: MouseEvent) => this.handleDrag(event.clientX);
  private readonly onTouchMove = (event: TouchEvent) => {
    if (event.cancelable) {
      event.preventDefault();
    }
    if (event.touches.length > 0) {
      this.handleDrag(event.touches[0].clientX);
    }
  };

  private readonly onStopResize = () => this.stopResize();
  private readonly mouseListenerOptions: AddEventListenerOptions = { capture: true };
  private readonly touchMoveListenerOptions: AddEventListenerOptions = { passive: false, capture: true };
  private readonly touchEndListenerOptions: AddEventListenerOptions = { capture: true };
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

  public getEnumValue(key: string): ContextEntryType {
    return this.contextEntryType[key as keyof typeof ContextEntryType];
  }

  public getEditorOptions(entry: ContextEntryEntity): any {
    if (entry.entryType() === ContextEntryType.JSON) {
      return MonacoService.editorOptionsJson;
    }

    return MonacoService.editorOptionsCSharp;
  }

  public ngOnInit(): void {
    this.tabs = [
      { id: 'input', title: 'Input', content: this.inputTab },
      { id: 'output', title: 'Output', content: this.outputTab },
      { id: 'assets', title: 'Assets', content: this.assetsTab },
      { id: 'trace', title: 'Trace', content: this.traceTab }
    ];

    this.loadStoredWidth();
    this.emitWidth();
  }

  public ngOnDestroy(): void {
    this.cleanupListeners();
  }

  public onActiveTabIdChange(tabId: string): void {
    this.activeTabId = tabId;
    this.activeTabIdChange.emit(tabId);
  }

  public startResize(event: MouseEvent | TouchEvent): void {
    event.preventDefault();
    if (this.isResizing) {
      return;
    }

    const clientX = this.getClientX(event);
    this.isResizing = true;
    this.startX = clientX;
    this.startWidth = this.tracebarWidth;
    window.addEventListener('mousemove', this.onMouseMove, this.mouseListenerOptions);
    window.addEventListener('mouseup', this.onStopResize, this.mouseListenerOptions);
    window.addEventListener('touchmove', this.onTouchMove, this.touchMoveListenerOptions);
    window.addEventListener('touchend', this.onStopResize, this.touchEndListenerOptions);
    window.addEventListener('touchcancel', this.onStopResize, this.touchEndListenerOptions);
  }

  private stopResize(): void {
    if (!this.isResizing) {
      return;
    }

    this.isResizing = false;
    this.cleanupListeners();
  }

  private handleDrag(clientX: number): void {
    if (!this.isResizing) {
      return;
    }

    const delta = this.startX - clientX;
    const nextWidth = this.clampWidth(this.startWidth + delta);

    if (nextWidth !== this.tracebarWidth) {
      this.tracebarWidth = nextWidth;
      this.saveWidth(nextWidth);
      this.emitWidth(nextWidth);
    }
  }

  private clampWidth(width: number): number {
    return Math.min(this.maxWidth, Math.max(this.minWidth, width));
  }

  private getClientX(event: MouseEvent | TouchEvent): number {
    if ('touches' in event) {
      return event.touches[0]?.clientX ?? this.startX;
    }

    return event.clientX;
  }

  private cleanupListeners(): void {
    window.removeEventListener('mousemove', this.onMouseMove, this.mouseListenerOptions);
    window.removeEventListener('mouseup', this.onStopResize, this.mouseListenerOptions);
    window.removeEventListener('touchmove', this.onTouchMove, this.touchMoveListenerOptions);
    window.removeEventListener('touchend', this.onStopResize, this.touchEndListenerOptions);
    window.removeEventListener('touchcancel', this.onStopResize, this.touchEndListenerOptions);
  }

  private loadStoredWidth(): void {
    if (!this.canUseLocalStorage()) {
      return;
    }

    try {
      const storedWidth = window.localStorage.getItem(this.storageKey);
      const parsedWidth = storedWidth ? Number(storedWidth) : NaN;

      if (!isNaN(parsedWidth)) {
        this.tracebarWidth = this.clampWidth(parsedWidth);
      }
    } catch {
    }
  }

  private saveWidth(width: number): void {
    if (!this.canUseLocalStorage()) {
      return;
    }

    try {
      window.localStorage.setItem(this.storageKey, width.toString());
    } catch {
    }
  }

  private canUseLocalStorage(): boolean {
    return typeof window !== 'undefined' && typeof window.localStorage !== 'undefined';
  }

  private emitWidth(width: number = this.tracebarWidth): void {
    this.tracebarWidthChange.emit(width);
  }

  public getSelectedAssetLabel(entry: ContextEntryEntity): string {
    return parseAssetRefValue(entry.entryValue())?.name ?? '';
  }

  public getSelectedAssetListLabel(entry: ContextEntryEntity): string {
    const assets = parseAssetRefListValue(entry.entryValue());
    if (assets.length === 0) {
      return '';
    }

    if (assets.length <= 3) {
      return assets.map(asset => asset.name).join(', ');
    }

    return `${assets.length} assets selected`;
  }

  public openAssetPicker(entry: ContextEntryEntity, mode: 'single' | 'multi'): void {
    const selected = parseAssetRefValue(entry.entryValue());
    const initialSelection = mode === 'single'
      ? (selected ? [selected] : [])
      : parseAssetRefListValue(entry.entryValue());

    this.dialogService.open(AssetPickerDialogComponent, {
      allowStack: true,
      mode,
      title: mode === 'single' ? 'Select asset' : 'Select assets',
      initialSelection,
      onSelect: (assets: AssetRef[]) => {
        if (mode === 'single') {
          entry.entryValue.set(buildAssetRefValue(assets[0] ?? null));
          return;
        }

        entry.entryValue.set(buildAssetRefListValue(assets));
      }
    });
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

  private normalizeMediaType(mediaType: string | undefined | null): string {
    if (!mediaType) {
      return '';
    }

    return mediaType.split(';', 2)[0].trim().toLowerCase();
  }
}
