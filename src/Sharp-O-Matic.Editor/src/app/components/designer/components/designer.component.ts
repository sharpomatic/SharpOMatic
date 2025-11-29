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

  public NodeStatus = NodeStatus;
  public NodeType = NodeType;

  private isSurfaceEnabled = false;
  private isSurfaceDragging = false;
  private isNodeEnabled = false;
  private isNodeDragging = false;
  private isConnectorDragging = false;
  private dragStartPoint: Point = { x: 0, y: 0 };
  private dragStartSelections: Entity<EntitySnapshot>[] = [];
  private dragStartPositions: Point[] = [];
  private selectionNode: NodeEntity<NodeSnapshot> | null = null;
  private selectionConnector: ConnectorEntity | null = null;

  private getMouseInSurfaceUnits(event: MouseEvent): Point {
    const surfaceRect = this.designerSurface.nativeElement.getBoundingClientRect();
    return { x: event.clientX - surfaceRect.left, y: event.clientY - surfaceRect.top };
  }

  onSurfaceMouseDown(event: MouseEvent): void {
    event.stopPropagation();

    if (event.button !== 0) return;

    if (!event.ctrlKey) {
      this.selectionService.clearSelection();
    }

    this.isSurfaceEnabled = true;
    this.dragStartPoint = this.getMouseInSurfaceUnits(event);
  }

  onSurfaceMove(event: MouseEvent): void {
    event.stopPropagation();

    const mousePoint = this.getMouseInSurfaceUnits(event);
    const deltaX = mousePoint.x - this.dragStartPoint.x;
    const deltaY = mousePoint.y - this.dragStartPoint.y;

    if (this.isSurfaceEnabled) {
      if (!this.isSurfaceDragging) {
        const maxDelta = Math.max(Math.abs(deltaX), Math.abs(deltaY));
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

      const left = Math.min(this.dragStartPoint.x, mousePoint.x);
      const top = Math.min(this.dragStartPoint.y, mousePoint.y);
      const width = Math.abs(this.dragStartPoint.x - mousePoint.x);
      const height = Math.abs(this.dragStartPoint.y - mousePoint.y);

      this.selectionRect.set({ left, top, width, height });

      const workflow = this.workflow();
      const selectedNodes = workflow.nodes()
        .filter(node => {
          return (
            node.left() >= left &&
            node.left() + node.width() <= left + width &&
            node.top() >= top &&
            node.top() + node.height() <= top + height
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
        const maxDelta = Math.max(Math.abs(deltaX), Math.abs(deltaY));
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
        let newX = Math.round((point.x + deltaX) / DesignerUpdateService.GRID_SIZE) * DesignerUpdateService.GRID_SIZE;
        let newY = Math.round((point.y + deltaY) / DesignerUpdateService.GRID_SIZE) * DesignerUpdateService.GRID_SIZE;
        return { x: newX, y: newY };
      });

      this.updateService.updateNodePositions(this.workflow(), this.dragStartSelections as NodeEntity<NodeSnapshot>[], nodePositions);
      return
    }

    if (this.isConnectorDragging && this.selectionConnector) {
      this.pendingConnection.set({ from: this.selectionConnector, targetPoint: mousePoint });
    }
  }

  onSurfaceMouseUp(event: MouseEvent): void {
    event.stopPropagation();

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
    this.dragStartPoint = this.getMouseInSurfaceUnits(event);

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
      this.dragStartPoint = this.getMouseInSurfaceUnits(event);
      this.pendingConnection.set({ from: connector, targetPoint: this.dragStartPoint });
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
      startY = (fromNode.top() + fromConnector.boxOffset() + ConnectorEntity.DISPLAY_SIZE - 5);
      endX = toNode.left() - 25;
      endY = (toNode.top() + toConnector.boxOffset() + ConnectorEntity.DISPLAY_SIZE - 5);
    } else {
      const fromNode = workflow.getNodeById(connection.from.nodeId);

      if (!fromNode) {
        return '';
      }

      startX = (fromNode.left() + fromNode.width()) + 10;
      startY = (fromNode.top() + connection.from.boxOffset() + ConnectorEntity.DISPLAY_SIZE - 5);
      endX = connection.targetPoint.x - 25;
      endY = connection.targetPoint.y + 2;
    }

    const controlPointOffset = 80; // Adjust this value for more or less curve
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
}
