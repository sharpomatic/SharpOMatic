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
import { EndNodeEntity } from '../../entities/definitions/end-node.entity';
import { CommonModule } from '@angular/common';
import { ContextEntryPurpose } from '../../entities/enumerations/context-entry-purpose';
import { ContextEntryEntity } from '../../entities/definitions/context-entry.entity';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';

@Component({
  selector: 'app-end-node-dialog',
  imports: [CommonModule, FormsModule, TabComponent, ContextViewerComponent],
  templateUrl: './end-node-dialog.component.html',
  styleUrls: ['./end-node-dialog.component.scss'],
})
export class EndNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: EndNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';

  constructor(
    @Inject(DIALOG_DATA)
    data: {
      node: EndNodeEntity;
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

  onAppendEntry(): void {
    this.node.mappings().appendEntry({ purpose: ContextEntryPurpose.Output });
  }

  onDeleteEntry(entryId: string): void {
    this.node.mappings().deleteEntry(entryId);
  }

  canMoveEntryUp(entry: ContextEntryEntity): boolean {
    return this.hasSiblingEntry(entry, -1);
  }

  canMoveEntryDown(entry: ContextEntryEntity): boolean {
    return this.hasSiblingEntry(entry, 1);
  }

  onMoveEntryUp(entry: ContextEntryEntity): void {
    this.node.mappings().moveEntry(entry.id, 'up', entry.purpose());
  }

  onMoveEntryDown(entry: ContextEntryEntity): void {
    this.node.mappings().moveEntry(entry.id, 'down', entry.purpose());
  }

  onClose(): void {
    this.close.emit();
  }

  private hasSiblingEntry(entry: ContextEntryEntity, step: number): boolean {
    const entries = this.node.mappings().entries();
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
