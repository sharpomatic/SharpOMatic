import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnInit,
  Output,
  SimpleChanges,
  TemplateRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';

export interface TabItem {
  id: string;
  title: string;
  content?: string | TemplateRef<unknown>;
}

@Component({
  selector: 'app-tab',
  templateUrl: './tab.component.html',
  styleUrls: ['./tab.component.scss'],
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TabComponent implements OnInit, OnChanges {
  @Input() tabs: TabItem[] = [];
  @Input() activeTabId?: string;
  @Input() preventOverflow = false;
  @Input() contentClass = 'bg-body-tertiary';
  @Output() activeTabChange = new EventEmitter<TabItem>();
  @Output() activeTabIdChange = new EventEmitter<string>();

  activeTab?: TabItem;

  ngOnInit() {
    this.syncActiveTab();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['tabs'] || changes['activeTabId']) {
      this.syncActiveTab();
    }
  }

  selectTab(tab: TabItem) {
    if (this.activeTab?.id === tab.id) return;
    this.activeTab = tab;
    this.activeTabId = tab.id;
    this.activeTabChange.emit(tab);
    this.activeTabIdChange.emit(tab.id);
  }

  isTemplateRef(content: TabItem['content']): content is TemplateRef<unknown> {
    return content instanceof TemplateRef;
  }

  private syncActiveTab(): void {
    if (!this.tabs?.length) {
      this.activeTab = undefined;
      return;
    }

    const next =
      this.tabs.find((t) => t.id === this.activeTabId) ?? this.tabs[0];
    this.activeTab = next;
    this.activeTabId = next.id;
  }
}
