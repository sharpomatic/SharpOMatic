import { ChangeDetectionStrategy, Component, ElementRef, HostListener, inject, signal, ViewChild, WritableSignal, input, Signal, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DesignerUpdateService } from '../services/designer-update.service';
import { Point } from '../interfaces/point';
import { Rect } from '../interfaces/rect';
import { DesignerSelectionService } from '../services/designer-selection.service';
import { DialogService } from '../../../dialogs/services/dialog.service';
import { EditNodeDialogComponent } from '../../../dialogs/edit-node/edit-node-dialog.component';
import { ConnectorEntity } from '../../../entities/definitions/connector.entity';
import { Entity, EntitySnapshot } from '../../../entities/definitions/entity.entity';
import { NodeEntity, NodeSnapshot } from '../../../entities/definitions/node.entity';
import { EditNodeEntity } from '../../../entities/definitions/edit-node.entity';
import { ConnectionEntity } from '../../../entities/definitions/connection.entity';
import { CodeNodeEntity } from '../../../entities/definitions/code-node.entity';
import { CodeNodeDialogComponent } from '../../../dialogs/code-node/code-node-dialog.component';
import { ModelCallNodeEntity } from '../../../entities/definitions/model-call-node.entity';
import { ModelCallNodeDialogComponent } from '../../../dialogs/model-call-node/model-call-node-dialog.component';
import { EndNodeEntity } from '../../../entities/definitions/end-node.entity';
import { StartNodeDialogComponent } from '../../../dialogs/start-node/start-node-dialog.component';
import { StartNodeEntity } from '../../../entities/definitions/start-node.entity';
import { EndNodeDialogComponent } from '../../../dialogs/end-node/end-node-dialog.component';
import { NodeStatus } from '../../../enumerations/node-status';
import { NodeType, getNodeSymbol } from '../../../entities/enumerations/node-type';
import { WorkflowEntity } from '../../../entities/definitions/workflow.entity';
import { SwitchNodeDialogComponent } from '../../../dialogs/switch-node/switch-node-dialog.component';
import { SwitchNodeEntity } from '../../../entities/definitions/switch-node.entity';
import { FanInNodeDialogComponent } from '../../../dialogs/fan-in-node/fan-in-node-dialog.component';
import { FanOutNodeDialogComponent } from '../../../dialogs/fan-out-node/fan-out-node-dialog.component';
import { FanInNodeEntity } from '../../../entities/definitions/fan-in-node.entity';
import { FanOutNodeEntity } from '../../../entities/definitions/fan-out-node.entity';
import { TraceProgressModel } from '../../../pages/workflow/interfaces/trace-progress-model';

@Component({
  selector: 'app-designer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './designer.component.html',
  styleUrls: ['./designer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DesignerComponent {
  @ViewChild('designerSurface') designerSurface!: ElementRef<HTMLDivElement>;
  @Input() workflow!: Signal<WorkflowEntity>;
  @Input() traces!: Signal<TraceProgressModel[]>;
  private readonly updateService = inject(DesignerUpdateService);
  private readonly selectionService = inject(DesignerSelectionService);
  private readonly dialogService = inject(DialogService);

  public readonly selectedEntities = this.selectionService.selectedEntities;
  public selectionRect: WritableSignal<Rect | null> = signal(null);
  public pendingConnection: WritableSignal<{ from: ConnectorEntity, targetPoint: Point } | null> = signal(null);
  public panOffset: WritableSignal<Point> = signal({ x: 0, y: 0 });
  public zoom: WritableSignal<number> = signal(1);

  public NodeStatus = NodeStatus;
  public NodeType = NodeType;

  private readonly minZoom = 0.25;
  private readonly maxZoom = 3;
  private isSurfaceEnabled = false;
  private isSurfaceDragging = false;
  private isNodeEnabled = false;
  private isNodeDragging = false;
  private isConnectorDragging = false;
  private isPanning = false;
  private suppressContextMenu = false;
  private dragStartViewPoint: Point = { x: 0, y: 0 };
  private dragStartWorldPoint: Point = { x: 0, y: 0 };
  private dragStartSelections: Entity<EntitySnapshot>[] = [];
  private dragStartPositions: Point[] = [];
  private panStartViewPoint: Point = { x: 0, y: 0 };
  private panStartOffset: Point = { x: 0, y: 0 };
  private selectionNode: NodeEntity<NodeSnapshot> | null = null;
  private selectionConnector: ConnectorEntity | null = null;

  private getMouseViewPoint(event: MouseEvent): Point {
    const surfaceRect = this.designerSurface.nativeElement.getBoundingClientRect();
    return { x: event.clientX - surfaceRect.left, y: event.clientY - surfaceRect.top };
  }

  private getMouseWorldPoint(event: MouseEvent): Point {
    const viewPoint = this.getMouseViewPoint(event);
    return this.viewToWorld(viewPoint);
  }

  private viewToWorld(viewPoint: Point): Point {
    const offset = this.panOffset();
    const zoom = this.zoom();
    return {
      x: (viewPoint.x - offset.x) / zoom,
      y: (viewPoint.y - offset.y) / zoom,
    };
  }

  onSurfaceMouseDown(event: MouseEvent): void {
    event.stopPropagation();

    if (event.button === 2) {
      event.preventDefault();
      this.isPanning = true;
      this.suppressContextMenu = true;
      this.panStartViewPoint = this.getMouseViewPoint(event);
      this.panStartOffset = this.panOffset();
      return;
    }

    if (event.button !== 0) return;

    if (!event.ctrlKey) {
      this.selectionService.clearSelection();
    }

    this.isSurfaceEnabled = true;
    this.dragStartViewPoint = this.getMouseViewPoint(event);
  }

  onSurfaceMove(event: MouseEvent): void {
    event.stopPropagation();

    const mouseViewPoint = this.getMouseViewPoint(event);
    const mouseWorldPoint = this.getMouseWorldPoint(event);

    if (this.isPanning) {
      this.setPanOffset({
        x: this.panStartOffset.x + (mouseViewPoint.x - this.panStartViewPoint.x),
        y: this.panStartOffset.y + (mouseViewPoint.y - this.panStartViewPoint.y),
      });
      return;
    }

    const deltaXView = mouseViewPoint.x - this.dragStartViewPoint.x;
    const deltaYView = mouseViewPoint.y - this.dragStartViewPoint.y;

    if (this.isSurfaceEnabled) {
      if (!this.isSurfaceDragging) {
        const maxDelta = Math.max(Math.abs(deltaXView), Math.abs(deltaYView));
        if (maxDelta < DesignerUpdateService.GRID_SIZE)
          return;

        this.isSurfaceDragging = true;

        if (!event.ctrlKey) {
          this.dragStartSelections = [];
          this.selectionService.clearSelection();
        } else {
          this.dragStartSelections = this.selectedEntities();
        }
      }

      const left = Math.min(this.dragStartViewPoint.x, mouseViewPoint.x);
      const top = Math.min(this.dragStartViewPoint.y, mouseViewPoint.y);
      const width = Math.abs(this.dragStartViewPoint.x - mouseViewPoint.x);
      const height = Math.abs(this.dragStartViewPoint.y - mouseViewPoint.y);

      this.selectionRect.set({ left, top, width, height });

      const workflow = this.workflow();
      const pan = this.panOffset();
      const zoom = this.zoom();
      const selectedNodes = workflow.nodes()
        .filter(node => {
          const nodeLeft = (node.left() * zoom) + pan.x;
          const nodeTop = (node.top() * zoom) + pan.y;
          const nodeRight = nodeLeft + node.width() * zoom;
          const nodeBottom = nodeTop + node.height() * zoom;
          return (
            nodeLeft >= left &&
            nodeRight <= left + width &&
            nodeTop >= top &&
            nodeBottom <= top + height
          );
        });

      const nodeIds = selectedNodes.map(n => n.id);
      const selectedConnections = workflow.connections()
        .filter(connection => {
          return (
            nodeIds.includes(connection.from()) ||
            nodeIds.includes(connection.to())
          );
        });

      this.selectionService.setSelection([...this.dragStartSelections, ...selectedNodes, ...selectedConnections]);
      return;
    }

    if (this.isNodeEnabled) {
      if (!this.isNodeDragging) {
        const deltaXWorld = mouseWorldPoint.x - this.dragStartWorldPoint.x;
        const deltaYWorld = mouseWorldPoint.y - this.dragStartWorldPoint.y;
        const maxDelta = Math.max(Math.abs(deltaXWorld), Math.abs(deltaYWorld));
        if (maxDelta < DesignerUpdateService.GRID_SIZE)
          return;

        const selectedEntity = this.selectionNode as Entity<EntitySnapshot>;
        if (!this.selectionService.isSelected(selectedEntity)) {
          if (event.ctrlKey) {
            this.selectionService.selectEntities([selectedEntity]);
          } else {
            this.selectionService.setSelection([selectedEntity]);
          }
        }

        this.isNodeDragging = true;
        this.dragStartSelections = this.selectedEntities().filter(entity => entity instanceof NodeEntity) as Entity<EntitySnapshot>[];
        this.dragStartPositions = this.dragStartSelections.map(entity => {
          const node = entity as NodeEntity<NodeSnapshot>;
          return { x: node.left(), y: node.top() }
        });
      }

      const nodePositions = this.dragStartPositions.map(point => {
        let newX = Math.round((point.x + (mouseWorldPoint.x - this.dragStartWorldPoint.x)) / DesignerUpdateService.GRID_SIZE) * DesignerUpdateService.GRID_SIZE;
        let newY = Math.round((point.y + (mouseWorldPoint.y - this.dragStartWorldPoint.y)) / DesignerUpdateService.GRID_SIZE) * DesignerUpdateService.GRID_SIZE;
        return { x: newX, y: newY };
      });

      this.updateService.updateNodePositions(this.workflow(), this.dragStartSelections as NodeEntity<NodeSnapshot>[], nodePositions);
      return
    }

    if (this.isConnectorDragging && this.selectionConnector) {
      this.pendingConnection.set({ from: this.selectionConnector, targetPoint: mouseWorldPoint });
    }
  }

  onSurfaceMouseUp(event: MouseEvent): void {
    event.stopPropagation();

    if (this.isPanning) {
      this.isPanning = false;
      this.suppressContextMenu = false;
      return;
    }

    if (this.isSurfaceEnabled) {
      this.isSurfaceEnabled = false;
      this.isSurfaceDragging = false;
      this.selectionRect.set(null);
      return;
    }

    if (this.isNodeEnabled) {
      this.isNodeEnabled = false;
      this.isNodeDragging = false;
      this.dragStartSelections = [];
      this.dragStartPositions = [];
      return;
    }

    if (this.isConnectorDragging) {
      this.isConnectorDragging = false;
      this.pendingConnection.set(null);
      return;
    }
  }

  onNodeMouseDown(node: NodeEntity<NodeSnapshot>, event: MouseEvent): void {
    event.stopPropagation();
    if (event.button !== 0) return;

    this.isNodeEnabled = true;
    this.selectionNode = node;
    this.dragStartWorldPoint = this.getMouseWorldPoint(event);

    if (event.ctrlKey) {
      if (!this.selectionService.isSelected(node))
        this.selectionService.selectEntities([node]);
      else
        this.selectionService.deselectEntities([node]);
    }
  }

  onNodeMouseUp(node: NodeEntity<NodeSnapshot>, event: MouseEvent): void {
    if (this.isNodeEnabled && !this.isNodeDragging) {
      if (!event.ctrlKey) {
        this.selectionService.clearSelection();
        this.selectionService.selectEntities([node]);
      }
    }

    if (this.isConnectorDragging) {
      this.isConnectorDragging = false;
      this.pendingConnection.set(null);
      return;
    }
  }

  onNodeDoubleClick(node: NodeEntity<NodeSnapshot>): void {
    const nodeTraces = this.traces().filter(t => t.nodeEntityId === node.id) ?? [];

    if (node instanceof StartNodeEntity) {
      this.dialogService.open(StartNodeDialogComponent, { node, nodeTraces });
    } else if (node instanceof EndNodeEntity) {
      this.dialogService.open(EndNodeDialogComponent, { node, nodeTraces });
    } else if (node instanceof CodeNodeEntity) {
      this.dialogService.open(CodeNodeDialogComponent, { node, nodeTraces });
    } else if (node instanceof ModelCallNodeEntity) {
      this.dialogService.open(ModelCallNodeDialogComponent, { node, nodeTraces });
    } else if (node instanceof EditNodeEntity) {
      this.dialogService.open(EditNodeDialogComponent, { node, nodeTraces });
    } else if (node instanceof SwitchNodeEntity) {
      this.dialogService.open(SwitchNodeDialogComponent, { node, nodeTraces });
    } else if (node instanceof FanInNodeEntity) {
      this.dialogService.open(FanInNodeDialogComponent, { node, nodeTraces });
    } else if (node instanceof FanOutNodeEntity) {
      this.dialogService.open(FanOutNodeDialogComponent, { node, nodeTraces });
    }
  }

  onInputMouseDown(connector: ConnectorEntity, event: MouseEvent): void {
    event.stopPropagation();
  }

  onInputMouseUp(connector: ConnectorEntity, event: MouseEvent): void {
    event.stopPropagation();

    if (this.isConnectorDragging) {
      this.isConnectorDragging = false;
      this.pendingConnection.set(null);
      this.updateService.addConnection(this.workflow(), this.selectionConnector as ConnectorEntity, connector);
    }
  }

  onOutputMouseDown(connector: ConnectorEntity, event: MouseEvent): void {
    event.stopPropagation();

    if (event.button !== 0) return;

    if (this.workflow().connections().find(c => c.from() === connector.id) === undefined) {
      this.isConnectorDragging = true;
      this.selectionConnector = connector;
      this.dragStartWorldPoint = this.getMouseWorldPoint(event);
      this.pendingConnection.set({ from: connector, targetPoint: this.dragStartWorldPoint });
    }
  }

  onOutputMouseUp(connector: ConnectorEntity, event: MouseEvent): void {
    event.stopPropagation();

    if (this.isConnectorDragging) {
      this.isConnectorDragging = false;
      this.pendingConnection.set(null);
    }
  }

  onConnectionMouseDown(connection: ConnectionEntity, event: MouseEvent): void {
    event.stopPropagation();
    if (event.button !== 0) return;

    if (event.ctrlKey) {
      if (!this.selectionService.isSelected(connection))
        this.selectionService.selectEntities([connection]);
      else
        this.selectionService.deselectEntities([connection]);
    } else {
      this.selectionService.setSelection([connection]);
    }
  }

  getConnectionCurve(connection: ConnectionEntity | { from: ConnectorEntity, targetPoint: Point }): string {
    let startX: number;
    let startY: number;
    let endX: number;
    let endY: number;

    const workflow = this.workflow();

    if (connection instanceof ConnectionEntity) {
      const fromConnector = workflow.getConnectorById(connection.from());
      const toConnector = workflow.getConnectorById(connection.to());

      if (!fromConnector || !toConnector) {
        return '';
      }

      const fromNode = workflow.getNodeById(fromConnector.nodeId);
      const toNode = workflow.getNodeById(toConnector.nodeId);

      if (!fromNode || !toNode) {
        return '';
      }

      startX = (fromNode.left() + fromNode.width()) + 6;
      startY = (fromNode.top() + fromConnector.boxOffset() + ConnectorEntity.DISPLAY_SIZE / 2) + 2;
      endX = toNode.left() - 25;
      endY = (toNode.top() + toConnector.boxOffset() + ConnectorEntity.DISPLAY_SIZE / 2) + 2;
    } else {
      const fromNode = workflow.getNodeById(connection.from.nodeId);

      if (!fromNode) {
        return '';
      }

      startX = (fromNode.left() + fromNode.width()) + 10;
      startY = (fromNode.top() + connection.from.boxOffset() + ConnectorEntity.DISPLAY_SIZE / 2) + 2;
      endX = connection.targetPoint.x - 25;
      endY = connection.targetPoint.y + 2;
    }

    const controlPointOffset = 20; // Adjust this value for more or less curve
    const controlX1 = startX + controlPointOffset;
    const controlY1 = startY;
    const controlX2 = endX - controlPointOffset;
    const controlY2 = endY;

    return `M${startX},${startY} C${controlX1},${controlY1} ${controlX2},${controlY2} ${endX},${endY}`;
  }

  @HostListener('window:keydown.delete', ['$event'])
  onDeleteKeyPressed(event: Event): void {
    this.updateService.deleteSelected(this.workflow());
  }

  public getNodeSymbol(nodeType: NodeType): string {
    return getNodeSymbol(nodeType);
  }

  public getPanTransform(): string {
    const offset = this.panOffset();
    return `translate(${offset.x}px, ${offset.y}px)`;
  }

  public getZoomTransform(): string {
    return `scale(${this.zoom()})`;
  }

  public isPanDragging(): boolean {
    return this.isPanning;
  }

  onSurfaceContextMenu(event: MouseEvent): void {
    event.preventDefault();
    this.suppressContextMenu = false;
  }

  public resetPanOffset(): void {
    this.resetView();
  }

  onSurfaceWheel(event: WheelEvent): void {
    event.preventDefault();
    event.stopPropagation();

    const anchorView = this.getMouseViewPoint(event);
    const zoomFactor = event.deltaY < 0 ? 1.1 : 0.9;
    this.applyZoom(zoomFactor, anchorView);
  }

  onSurfaceLeave(): void {
    if (this.isPanning) {
      this.isPanning = false;
      this.suppressContextMenu = false;
    }
  }

  private setPanOffset(offset: Point): void {
    const clamped = this.clampPanOffset(offset);
    this.panOffset.set(clamped);
  }

  private clampZoom(value: number): number {
    return Math.min(this.maxZoom, Math.max(this.minZoom, value));
  }

  private clampPanOffset(offset: Point): Point {
    const nodes = this.workflow().nodes();
    if (!nodes.length) {
      return {
        x: Math.min(offset.x, 3000),
        y: Math.min(offset.y, 3000),
      };
    }

    const zoom = this.zoom();
    const maxLeft = Math.max(...nodes.map(n => n.left()));
    const maxTop = Math.max(...nodes.map(n => n.top()));

    return {
      x: Math.min(Math.max(offset.x, -maxLeft * zoom), 3000),
      y: Math.min(Math.max(offset.y, -maxTop * zoom), 3000),
    };
  }

  public zoomIn(): void {
    this.applyZoom(1.1, this.getSurfaceCenterViewPoint());
  }

  public zoomOut(): void {
    this.applyZoom(0.9, this.getSurfaceCenterViewPoint());
  }

  public resetView(): void {
    this.zoom.set(1);
    this.setPanOffset({ x: 0, y: 0 });
  }

  private applyZoom(factor: number, anchorViewPoint: Point): void {
    const currentZoom = this.zoom();
    const worldPoint = this.viewToWorld(anchorViewPoint);
    const newZoom = this.clampZoom(currentZoom * factor);

    if (newZoom === currentZoom) {
      return;
    }

    this.zoom.set(newZoom);

    const newPan: Point = {
      x: anchorViewPoint.x - worldPoint.x * newZoom,
      y: anchorViewPoint.y - worldPoint.y * newZoom,
    };

    this.setPanOffset(newPan);
  }

  private getSurfaceCenterViewPoint(): Point {
    const surfaceRect = this.designerSurface.nativeElement.getBoundingClientRect();
    return { x: surfaceRect.width / 2, y: surfaceRect.height / 2 };
  }
}
