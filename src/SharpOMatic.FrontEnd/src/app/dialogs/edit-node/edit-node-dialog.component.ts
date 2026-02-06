import {
  Component,
  EventEmitter,
  Inject,
  OnInit,
  Output,
  TemplateRef,
  ViewChild,
} from '@angular/core';
import { DIALOG_DATA, DialogService } from '../services/dialog.service';
import { FormsModule } from '@angular/forms';
import { EditNodeEntity } from '../../entities/definitions/edit-node.entity';
import { ContextEntryEntity } from '../../entities/definitions/context-entry.entity';
import { ContextEntryType } from '../../entities/enumerations/context-entry-type';
import {
  AssetRef,
  buildAssetRefListValue,
  buildAssetRefValue,
  parseAssetRefListValue,
  parseAssetRefValue,
} from '../../entities/definitions/asset-ref';
import { CommonModule } from '@angular/common';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { MonacoService } from '../../services/monaco.service';
import { ContextEntryPurpose } from '../../entities/enumerations/context-entry-purpose';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { AssetPickerDialogComponent } from '../asset-picker/asset-picker-dialog.component';

@Component({
  selector: 'app-edit-node-dialog',
  imports: [
    CommonModule,
    FormsModule,
    MonacoEditorModule,
    TabComponent,
    ContextViewerComponent,
  ],
  templateUrl: './edit-node-dialog.component.html',
  styleUrls: ['./edit-node-dialog.component.scss'],
})
export class EditNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: EditNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public contextEntryType = ContextEntryType;
  public contextEntryPurpose = ContextEntryPurpose;
  public contextEntryTypeKeys: string[] = [];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  constructor(
    @Inject(DIALOG_DATA)
    data: { node: EditNodeEntity; nodeTraces: TraceProgressModel[] },
    private readonly dialogService: DialogService,
  ) {
    this.node = data.node;
    this.inputTraces = (data.nodeTraces ?? [])
      .map((trace) => trace.inputContext)
      .filter((context): context is string => context != null);
    this.outputTraces = (data.nodeTraces ?? [])
      .map((trace) => trace.outputContext)
      .filter((context): context is string => context != null);
    this.contextEntryTypeKeys = Object.keys(this.contextEntryType).filter((k) =>
      isNaN(Number(k)),
    );
  }

  ngOnInit(): void {
    this.tabs = [
      { id: 'details', title: 'Details', content: this.detailsTab },
      { id: 'inputs', title: 'Inputs', content: this.inputsTab },
      { id: 'outputs', title: 'Outputs', content: this.outputsTab },
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
      case 'AssetRef':
        return 'asset';
      case 'AssetRefList':
        return 'asset list';
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

  onInsertUpsertEntry(): void {
    const entries = this.node.edits().entries();
    const deleteIndex = entries.findIndex(
      (entry) => entry.purpose() === ContextEntryPurpose.Delete,
    );
    const targetIndex = deleteIndex === -1 ? entries.length : deleteIndex;
    this.node
      .edits()
      .insertEntry(targetIndex, { purpose: ContextEntryPurpose.Upsert });
  }

  onAppendDeleteEntry(): void {
    this.node.edits().appendEntry({ purpose: ContextEntryPurpose.Delete });
  }

  onDeleteEntry(entryId: string): void {
    this.node.edits().deleteEntry(entryId);
  }

  canMoveEntryUp(entry: ContextEntryEntity): boolean {
    return this.hasSiblingEntry(entry, -1);
  }

  canMoveEntryDown(entry: ContextEntryEntity): boolean {
    return this.hasSiblingEntry(entry, 1);
  }

  onMoveEntryUp(entry: ContextEntryEntity): void {
    this.node.edits().moveEntry(entry.id, 'up', entry.purpose());
  }

  onMoveEntryDown(entry: ContextEntryEntity): void {
    this.node.edits().moveEntry(entry.id, 'down', entry.purpose());
  }

  onClose(): void {
    this.close.emit();
  }

  getSelectedAssetLabel(entry: ContextEntryEntity): string {
    return parseAssetRefValue(entry.entryValue())?.name ?? '';
  }

  getSelectedAssetListLabel(entry: ContextEntryEntity): string {
    const assets = parseAssetRefListValue(entry.entryValue());
    if (assets.length === 0) {
      return '';
    }

    if (assets.length <= 3) {
      return assets.map((asset) => asset.name).join(', ');
    }

    return `${assets.length} assets selected`;
  }

  openAssetPicker(entry: ContextEntryEntity, mode: 'single' | 'multi'): void {
    const selected = parseAssetRefValue(entry.entryValue());
    const initialSelection =
      mode === 'single'
        ? selected
          ? [selected]
          : []
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
      },
    });
  }

  hasUpsertEntries(): boolean {
    return this.node
      .edits()
      .entries()
      .some((entry) => entry.purpose() === ContextEntryPurpose.Upsert);
  }

  hasDeleteEntries(): boolean {
    return this.node
      .edits()
      .entries()
      .some((entry) => entry.purpose() === ContextEntryPurpose.Delete);
  }

  private hasSiblingEntry(entry: ContextEntryEntity, step: number): boolean {
    const entries = this.node.edits().entries();
    const index = entries.findIndex((e) => e.id === entry.id);
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
