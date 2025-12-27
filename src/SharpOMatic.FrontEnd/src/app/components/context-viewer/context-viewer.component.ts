import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, SimpleChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface ContextNode {
  name: string;
  type: string;
  displayValue: string;
  children: ContextNode[];
  expanded: boolean;
}

const CONTEXT_OBJECT = 'ContextObject';
const CONTEXT_LIST = 'ContextList';

@Component({
  selector: 'app-context-viewer',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './context-viewer.component.html',
  styleUrls: ['./context-viewer.component.scss']
})
export class ContextViewerComponent implements OnChanges {
  @Input() contexts: string[] = [];
  @Input() mode: 'full' | 'simple' = 'full';

  public selectedIndex = signal(0);
  public contextTree = signal<ContextNode[]>([]);
  public errorMessage = signal<string | null>(null);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['contexts']) {
      this.resetSelection();
      this.refreshRows();
    }
  }

  onSelectedIndexChange(index: number): void {
    this.selectedIndex.set(index);
    this.refreshRows();
  }

  private resetSelection(): void {
    const hasContexts = this.contexts?.length > 0;
    if (!hasContexts) {
      this.selectedIndex.set(-1);
      return;
    }

    const index = this.selectedIndex();
    if (index < 0 || index >= this.contexts.length) {
      this.selectedIndex.set(0);
    }
  }

  private refreshRows(): void {
    const contexts = this.contexts ?? [];
    const index = this.selectedIndex();

    if (contexts.length === 0) {
      this.contextTree.set([]);
      this.errorMessage.set('(empty)');
      return;
    }

    if (index < 0 || index >= contexts.length) {
      this.contextTree.set([]);
      this.errorMessage.set('No context selected.');
      return;
    }

    const rawContext = contexts[index];
    if (!rawContext || rawContext.trim() === '') {
      this.contextTree.set([]);
      this.errorMessage.set('(empty)');
      return;
    }

    try {
      const parsed = JSON.parse(rawContext);

      if (parsed === null || Array.isArray(parsed) || typeof parsed !== 'object') {
        this.contextTree.set([]);
        this.errorMessage.set('Context is not an object.');
        return;
      }

      const tree = this.buildNodesFromObject(parsed as Record<string, unknown>);
      this.contextTree.set(tree);
      this.errorMessage.set(null);
    } catch {
      this.contextTree.set([]);
      this.errorMessage.set('Unable to parse context JSON.');
    }
  }

  toggleNode(node: ContextNode): void {
    node.expanded = !node.expanded;
    this.contextTree.set([...this.contextTree()]);
  }

  private buildNodesFromObject(obj: Record<string, unknown>): ContextNode[] {
    return Object.entries(obj).map(([name, payload]) => this.toContextNode(name, payload));
  }

  private toContextNode(name: string, payload: unknown): ContextNode {
    const payloadObject = this.getPayloadObject(payload);
    const isContextObject = payloadObject.$type === CONTEXT_OBJECT;
    const isContextList = payloadObject.$type === CONTEXT_LIST;
    const isArray = Array.isArray(payloadObject.value);

    if (isContextObject && this.isPlainObject(payloadObject.value)) {
      const children = this.buildNodesFromObject(payloadObject.value as Record<string, unknown>);
      return {
        name,
        type: this.getPayloadType(payloadObject),
        displayValue: '',
        children,
        expanded: false
      };
    }

    if (isContextList && isArray) {
      const arrayValue = payloadObject.value as unknown[];
      const children = arrayValue.map((item, index) => this.toContextNode(`[${index}]`, item));
      const nameWithLength = `${name}[${arrayValue.length}]`;
      return {
        name: nameWithLength,
        type: this.getPayloadType(payloadObject),
        displayValue: '',
        children,
        expanded: false
      };
    }

    return {
      name,
      type: this.getPayloadType(payloadObject),
      displayValue: this.formatValue(payloadObject.value),
      children: [],
      expanded: false
    };
  }

  private getPayloadObject(payload: unknown): { $type?: unknown; value: unknown } {
    if (payload && typeof payload === 'object' && !Array.isArray(payload)) {
      const { $type, value } = payload as { $type?: unknown; value?: unknown };
      return { $type, value };
    }

    return { value: payload };
  }

  private isPlainObject(value: unknown): boolean {
    return value !== null && typeof value === 'object' && !Array.isArray(value);
  }

  private getPayloadType(payload: { $type?: unknown; value: unknown }): string {
    if (typeof payload.$type === 'string' && payload.$type.trim() !== '') {
      if (payload.$type === CONTEXT_OBJECT) return 'Object';
      if (payload.$type === CONTEXT_LIST) return 'Array';
      return payload.$type;
    }

    const value = payload.value;
    if (value === null) return 'null';
    if (Array.isArray(value)) return 'Array';
    if (value === undefined) return 'undefined';
    if (typeof value === 'object') return 'Object';
    return typeof value;
  }

  private formatValue(value: unknown): string {
    if (value === null) return 'null';
    if (value === undefined) return 'undefined';
    if (typeof value === 'string') return value;
    if (typeof value === 'number' || typeof value === 'boolean') return value.toString();

    try {
      return JSON.stringify(value);
    } catch {
      return '[unserializable]';
    }
  }
}
