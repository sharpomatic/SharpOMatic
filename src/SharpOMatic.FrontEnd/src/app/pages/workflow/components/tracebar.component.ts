import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, TemplateRef, ViewChild, computed, inject } from '@angular/core';
import { RunStatus } from '../../../enumerations/run-status';
import { WorkflowService } from '../services/workflow.service';
import { ContextEntryType } from '../../../entities/enumerations/context-entry-type';
import { FormsModule } from '@angular/forms';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { ContextEntryEntity } from '../../../entities/definitions/context-entry.entity';
import { MonacoService } from '../../../services/monaco.service';
import { TabComponent, TabItem } from '../../../components/tab/tab.component';
import { ContextViewerComponent } from '../../../components/context-viewer/context-viewer.component';
import { TraceViewerComponent } from '../../../components/trace-viewer/trace-viewer.component';

@Component({
  selector: 'app-tracebar',
  standalone: true,
  imports: [CommonModule, FormsModule, MonacoEditorModule, TabComponent, ContextViewerComponent, TraceViewerComponent],
  templateUrl: './tracebar.component.html',
  styleUrl: './tracebar.component.scss'
})
export class TracebarComponent implements OnInit, OnDestroy {
  @ViewChild('inputTab', { static: true }) inputTab!: TemplateRef<unknown>;
  @ViewChild('outputTab', { static: true }) outputTab!: TemplateRef<unknown>;
  @ViewChild('traceTab', { static: true }) traceTab!: TemplateRef<unknown>;
  @Output() public tracebarWidthChange = new EventEmitter<number>();

  public readonly workflowService = inject(WorkflowService);
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
}
