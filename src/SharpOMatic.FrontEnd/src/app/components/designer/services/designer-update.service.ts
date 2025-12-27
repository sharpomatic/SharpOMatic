import { inject, Injectable, signal } from '@angular/core';
import { Point } from '../interfaces/point';
import { DesignerSelectionService } from './designer-selection.service';
import { StartNodeEntity } from '../../../entities/definitions/start-node.entity';
import { EndNodeEntity } from '../../../entities/definitions/end-node.entity';
import { CodeNodeEntity } from '../../../entities/definitions/code-node.entity';
import { EditNodeEntity } from '../../../entities/definitions/edit-node.entity';
import { NodeEntity, NodeSnapshot } from '../../../entities/definitions/node.entity';
import { Entity, EntitySnapshot } from '../../../entities/definitions/entity.entity';
import { ConnectionEntity } from '../../../entities/definitions/connection.entity';
import { ConnectorEntity } from '../../../entities/definitions/connector.entity';
import { WorkflowEntity } from '../../../entities/definitions/workflow.entity';
import { SwitchNodeEntity } from '../../../entities/definitions/switch-node.entity';
import { FanInNodeEntity } from '../../../entities/definitions/fan-in-node.entity';
import { FanOutNodeEntity } from '../../../entities/definitions/fan-out-node.entity';
import { ModelCallNodeEntity } from '../../../entities/definitions/model-call-node.entity';

@Injectable({
  providedIn: 'root',
})
export class DesignerUpdateService {
  public static readonly GRID_SIZE = 16;

  private readonly selectionService = inject(DesignerSelectionService);

  private getAddLocation(): [number, number] {
    const top = Math.round((Math.random() * 300) / DesignerUpdateService.GRID_SIZE) * DesignerUpdateService.GRID_SIZE;
    const left = Math.round((Math.random() * 300) / DesignerUpdateService.GRID_SIZE) * DesignerUpdateService.GRID_SIZE;
    return [top, left];
  }

  addStartNode(workflow: WorkflowEntity) {
    const [top, left] = this.getAddLocation();
    workflow.nodes.update(nodes => [...nodes, StartNodeEntity.create(top, left)]);
  }

  addEndNode(workflow: WorkflowEntity) {
    const [top, left] = this.getAddLocation();
    workflow.nodes.update(nodes => [...nodes, EndNodeEntity.create(top, left)]);
  }

  addEditNode(workflow: WorkflowEntity) {
    const [top, left] = this.getAddLocation();
    workflow.nodes.update(nodes => [...nodes, EditNodeEntity.create(top, left)]);
  }

  addCodeNode(workflow: WorkflowEntity) {
    const [top, left] = this.getAddLocation();
    workflow.nodes.update(nodes => [...nodes, CodeNodeEntity.create(top, left)]);
  }

  addSwitchNode(workflow: WorkflowEntity) {
    const [top, left] = this.getAddLocation();
    workflow.nodes.update(nodes => [...nodes, SwitchNodeEntity.create(top, left)]);
  }

  addFanInNode(workflow: WorkflowEntity) {
    const [top, left] = this.getAddLocation();
    workflow.nodes.update(nodes => [...nodes, FanInNodeEntity.create(top, left)]);
  }

  addFanOutNode(workflow: WorkflowEntity) {
    const [top, left] = this.getAddLocation();
    workflow.nodes.update(nodes => [...nodes, FanOutNodeEntity.create(top, left)]);
  }

  addModelCallNode(workflow: WorkflowEntity) {
    const [top, left] = this.getAddLocation();
    workflow.nodes.update(nodes => [...nodes, ModelCallNodeEntity.create(top, left)]);
  }

  addConnection(workflow: WorkflowEntity, from: ConnectorEntity, to: ConnectorEntity) {
    if (from.id === to.id) {
      return;
    }

    workflow.connections.update(connections => [...connections, ConnectionEntity.create(from.id, to.id)]);
  }

  deleteNode(workflow: WorkflowEntity, node: NodeEntity<NodeSnapshot>) {
    const entities: Entity<EntitySnapshot>[] = [node];

    workflow.nodes.update(nodes => nodes.filter((n) => n.id !== node.id));
    workflow.connections.update(connections =>
      connections.filter(c => {
        const fromConnector = workflow.getConnectorById(c.from());
        const toConnector = workflow.getConnectorById(c.to());

        if (fromConnector?.nodeId === node.id || toConnector?.nodeId === node.id) {
          entities.push(c);
          return false;
        }

        return true;
      })
    );

    this.selectionService.deselectEntities(entities);
  }

  deleteConnection(workflow: WorkflowEntity, connection: ConnectionEntity) {
    workflow.connections.update(conns => conns.filter(c => c.id !== connection.id));
  }

  deleteSelected(workflow: WorkflowEntity): void {
    const entities = this.selectionService.selectedEntities();
    if (entities.length === 0) {
      return;
    }

    for (const entity of entities) {
      if (entity instanceof NodeEntity) {
        this.deleteNode(workflow, entity as NodeEntity<NodeSnapshot>);
      } else if (entity instanceof ConnectionEntity) {
        this.deleteConnection(workflow, entity as ConnectionEntity);
      }
    }
  }

  updateNodePositions(workflow: WorkflowEntity, updates: NodeEntity<NodeSnapshot>[], positions: Point[]) {
    workflow.nodes.update(nodes =>
      nodes.map(node => {
        const index = updates.findIndex((update, _) => (update === node));
        if (index >= 0) {
          node.left.set(positions[index].x);
          node.top.set(positions[index].y);
        }

        return node;
      }),
    );
  }

  deleteSwitch(workflow: WorkflowEntity, node: SwitchNodeEntity, switchId: string) {
    const switches = node.switches();
    const index = switches.findIndex(s => s.id === switchId);
    if (index === -1) return;

    const connector = node.outputs()[index];
    node.deleteSwitch(switchId);

    workflow.connections.update(conns => conns.filter(c => c.from() !== connector.id));
  }

  deleteFanOutOutput(workflow: WorkflowEntity, node: FanOutNodeEntity, index: number) {
    const outputs = node.outputs();
    if (index < 0 || index >= outputs.length) return;

    const connector = outputs[index];
    node.deleteOutput(index);

    workflow.connections.update(conns => conns.filter(c => c.from() !== connector.id));
  }
}
