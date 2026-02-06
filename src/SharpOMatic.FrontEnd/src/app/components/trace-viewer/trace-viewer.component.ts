import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { getNodeSymbol } from '../../entities/enumerations/node-type';
import { NodeStatus } from '../../enumerations/node-status';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';

interface TraceTreeNode {
  trace: TraceProgressModel;
  children: TraceTreeNode[];
  expanded: boolean;
}

@Component({
  selector: 'app-trace-viewer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './trace-viewer.component.html',
  styleUrls: ['./trace-viewer.component.scss'],
})
export class TraceViewerComponent implements OnChanges {
  @Input() traces: TraceProgressModel[] = [];
  public traceTree: TraceTreeNode[] = [];
  private readonly expandedState = new Map<string, boolean>();

  public readonly NodeStatus = NodeStatus;
  public readonly getNodeSymbol = getNodeSymbol;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['traces']) {
      this.traceTree = this.buildTraceTree(this.traces ?? []);
    }
  }

  toggleNode(node: TraceTreeNode): void {
    node.expanded = !node.expanded;
    this.expandedState.set(node.trace.traceId, node.expanded);
  }

  private buildTraceTree(traces: TraceProgressModel[]): TraceTreeNode[] {
    const nodes = traces.map((trace) => ({
      trace,
      children: [],
      expanded: this.expandedState.get(trace.traceId) ?? true,
    }));

    const nodeById = new Map<string, TraceTreeNode>();
    nodes.forEach((node) => nodeById.set(node.trace.traceId, node));

    const roots: TraceTreeNode[] = [];
    nodes.forEach((node) => {
      const parentId = node.trace.parentTraceId;
      if (parentId && nodeById.has(parentId)) {
        nodeById.get(parentId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });

    return roots;
  }
}
