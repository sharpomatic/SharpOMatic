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
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { TraceViewerComponent } from '../../components/trace-viewer/trace-viewer.component';
import { StreamViewerComponent } from '../../components/stream-viewer/stream-viewer.component';
import { DIALOG_DATA } from '../services/dialog.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ConversationSummaryModel } from '../../pages/workflow/interfaces/conversation-summary-model';
import { ConversationHistoryModel } from '../../pages/workflow/interfaces/conversation-history-model';
import { ConversationHistoryTurnModel } from '../../pages/workflow/interfaces/conversation-history-turn-model';
import { AssetSummary } from '../../pages/assets/interfaces/asset-summary';
import { ConversationStatus } from '../../enumerations/conversation-status';
import { formatByteSize } from '../../helper/format-size';
import { isTextLikeMediaType, normalizeMediaType } from '../../helper/asset-media-type';
import { AssetPreviewDialogComponent } from '../asset-preview/asset-preview-dialog.component';
import { AssetTextDialogComponent } from '../asset-text/asset-text-dialog.component';

interface ConversationPropertyRow {
  label: string;
  value: string;
  multiline?: boolean;
  status?: boolean;
  date?: boolean;
}

@Component({
  selector: 'app-conversation-viewer-dialog',
  standalone: true,
  imports: [
    CommonModule,
    TabComponent,
    ContextViewerComponent,
    TraceViewerComponent,
    StreamViewerComponent,
  ],
  templateUrl: './conversation-viewer-dialog.component.html',
  styleUrls: ['./conversation-viewer-dialog.component.scss'],
  providers: [BsModalService],
})
export class ConversationViewerDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('conversationTab', { static: true })
  conversationTab!: TemplateRef<unknown>;
  @ViewChild('inputTab', { static: true }) inputTab!: TemplateRef<unknown>;
  @ViewChild('outputTab', { static: true }) outputTab!: TemplateRef<unknown>;
  @ViewChild('traceTab', { static: true }) traceTab!: TemplateRef<unknown>;
  @ViewChild('streamTab', { static: true }) streamTab!: TemplateRef<unknown>;
  @ViewChild('assetsTab', { static: true }) assetsTab!: TemplateRef<unknown>;

  public readonly conversation: ConversationSummaryModel;
  public tabs: TabItem[] = [];
  public activeTabId = 'conversation';
  public conversationProperties: ConversationPropertyRow[] = [];
  public history: ConversationHistoryModel | null = null;
  public turns: ConversationHistoryTurnModel[] = [];
  public conversationAssets: AssetSummary[] = [];
  public isLoading = true;
  public readonly ConversationStatus = ConversationStatus;

  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);

  constructor(
    @Inject(DIALOG_DATA) data: { conversation: ConversationSummaryModel },
  ) {
    this.conversation = data.conversation;
  }

  ngOnInit(): void {
    this.tabs = [
      {
        id: 'conversation',
        title: 'Conversation',
        content: this.conversationTab,
      },
      { id: 'input', title: 'Input', content: this.inputTab },
      { id: 'output', title: 'Output', content: this.outputTab },
      { id: 'assets', title: 'Assets', content: this.assetsTab },
      { id: 'trace', title: 'Trace', content: this.traceTab },
      { id: 'stream', title: 'Stream', content: this.streamTab },
    ];

    this.conversationProperties = this.buildConversationProperties();
    this.loadHistory();
  }

  onClose(): void {
    this.close.emit();
  }

  public getTurnLabel(
    turnNumber: number | null | undefined,
    index: number,
  ): string {
    return `Turn ${turnNumber ?? index + 1}`;
  }

  public getConversationStatusLabel(status: ConversationStatus): string {
    return ConversationStatus[status] ?? status.toString();
  }

  public getInputContexts(turn: ConversationHistoryTurnModel): string[] {
    return turn.run.inputContext ? [turn.run.inputContext] : [];
  }

  public getOutputContexts(turn: ConversationHistoryTurnModel): string[] {
    return turn.run.outputContext ? [turn.run.outputContext] : [];
  }

  public formatSize(bytes: number): string {
    return formatByteSize(bytes);
  }

  public isImageAsset(asset: AssetSummary): boolean {
    const mediaType = normalizeMediaType(asset.mediaType);
    return mediaType.startsWith('image/');
  }

  public isViewableTextAsset(asset: AssetSummary): boolean {
    return isTextLikeMediaType(asset.mediaType);
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

  private loadHistory(): void {
    this.isLoading = true;
    this.serverRepository
      .getConversationHistory(this.conversation.conversationId)
      .subscribe((history) => {
        this.history = history;
        this.turns = this.sortTurns(history?.turns ?? []);
        this.conversationAssets = this.sortAssetsByCreated(
          history?.conversationAssets ?? [],
        );
        this.isLoading = false;
      });
  }

  private buildConversationProperties(): ConversationPropertyRow[] {
    return [
      {
        label: 'Created',
        value: this.conversation.created ?? '',
        date: true,
      },
      {
        label: 'Updated',
        value: this.conversation.updated ?? '',
        date: true,
      },
      {
        label: 'Status',
        value: this.getConversationStatusLabel(this.conversation.status),
        status: true,
      },
      {
        label: 'Turns',
        value: this.conversation.currentTurnNumber.toString(),
      },
      {
        label: 'Last Run Id',
        value: this.formatValue(this.conversation.lastRunId),
      },
      {
        label: 'Error',
        value: this.formatValue(this.conversation.lastError),
        multiline: true,
      },
    ];
  }

  private sortTurns(
    turns: ConversationHistoryTurnModel[],
  ): ConversationHistoryTurnModel[] {
    return [...turns]
      .map((turn) => ({
        ...turn,
        assets: this.sortAssetsByCreated(turn.assets ?? []),
      }))
      .sort((left, right) => {
        const leftTurn = left.run.turnNumber ?? 0;
        const rightTurn = right.run.turnNumber ?? 0;
        if (leftTurn !== rightTurn) {
          return leftTurn - rightTurn;
        }

        const leftTime = Date.parse(left.run.created);
        const rightTime = Date.parse(right.run.created);
        const normalizedLeft = Number.isFinite(leftTime) ? leftTime : 0;
        const normalizedRight = Number.isFinite(rightTime) ? rightTime : 0;
        return normalizedLeft - normalizedRight;
      });
  }

  private sortAssetsByCreated(assets: AssetSummary[]): AssetSummary[] {
    return [...assets].sort((left, right) => {
      const leftTime = Date.parse(left.created);
      const rightTime = Date.parse(right.created);
      const normalizedLeft = Number.isFinite(leftTime) ? leftTime : 0;
      const normalizedRight = Number.isFinite(rightTime) ? rightTime : 0;
      return normalizedLeft - normalizedRight;
    });
  }

  private formatValue(value?: string | null): string {
    const trimmed = (value ?? '').trim();
    return trimmed.length ? trimmed : '';
  }
}
