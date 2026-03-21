import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { InformationType } from '../../enumerations/information-type';
import { getNodeSymbol } from '../../entities/enumerations/node-type';
import { NodeStatus } from '../../enumerations/node-status';
import { InformationProgressModel } from '../../pages/workflow/interfaces/information-progress-model';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';

interface TraceTreeNode {
  trace: TraceProgressModel;
  informations: InformationProgressModel[];
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
  @Input() informations: InformationProgressModel[] = [];
  public traceTree: TraceTreeNode[] = [];
  private readonly expandedState = new Map<string, boolean>();

  public readonly NodeStatus = NodeStatus;
  public readonly InformationType = InformationType;
  public readonly getNodeSymbol = getNodeSymbol;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['traces'] || changes['informations']) {
      this.traceTree = this.buildTraceTree(this.traces ?? []);
    }
  }

  toggleNode(node: TraceTreeNode): void {
    node.expanded = !node.expanded;
    this.expandedState.set(node.trace.traceId, node.expanded);
  }

  private buildTraceTree(traces: TraceProgressModel[]): TraceTreeNode[] {
    const informationsByTraceId = new Map<string, InformationProgressModel[]>();
    (this.informations ?? []).forEach((information) => {
      const informations = informationsByTraceId.get(information.traceId) ?? [];
      informations.push(information);
      informationsByTraceId.set(information.traceId, informations);
    });

    const nodes = traces.map((trace) => ({
      trace,
      informations: informationsByTraceId.get(trace.traceId) ?? [],
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

  hasExpandableContent(node: TraceTreeNode): boolean {
    return node.children.length > 0 || node.informations.length > 0;
  }

  getInformationTypeLabel(type: InformationType): string {
    switch (type) {
      case InformationType.ToolCall:
        return 'Tool Call';
      case InformationType.Reasoning:
        return 'Reasoning';
      default:
        return 'Unknown';
    }
  }
}
