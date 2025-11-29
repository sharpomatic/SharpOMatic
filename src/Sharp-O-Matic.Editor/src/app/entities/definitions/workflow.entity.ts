import { computed, Signal, signal, WritableSignal } from '@angular/core';
import { Entity, EntitySnapshot } from './entity.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { ConnectionEntity, ConnectionSnapshot } from './connection.entity';
import { nodeFromSnapshot } from './node-factory';
import { ConnectorEntity } from './connector.entity';

export interface WorkflowSnapshot extends EntitySnapshot {
  name: string;
  description: string;
  nodes: NodeSnapshot[];
  connections: ConnectionSnapshot[];
}

export class WorkflowEntity extends Entity<WorkflowSnapshot> {
  private nodeMap: Map<string, NodeEntity<NodeSnapshot>> = new Map();
  private connectionMap: Map<string, ConnectionEntity> = new Map();
  private connectorMap: Map<string, ConnectorEntity> = new Map();

  public name: WritableSignal<string>;
  public description: WritableSignal<string>;
  public nodes: WritableSignal<NodeEntity<NodeSnapshot>[]>;
  public connections: WritableSignal<ConnectionEntity[]>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: WorkflowSnapshot) {
    super(snapshot);

    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.nodes = signal(snapshot.nodes.map(nodeFromSnapshot));
    this.connections = signal(snapshot.connections.map(ConnectionEntity.fromSnapshot));

    this.refreshCache();

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();
      const snaphotNodes = snapshot.nodes;
      const snaphotConnections = snapshot.connections;

      // Must touch all property signals
      const currentName = this.name();
      const currentDescription = this.description();
      const currentNodes = this.nodes();
      const currentConnections = this.connections();

      // Must touch all nodes/connections dirty signals
      const currentNodesDirty = currentNodes.reduce((dirty, node) => node.isDirty() || dirty, false);
      const currentConnectionsDirty = currentConnections.reduce((dirty, connection) => connection.isDirty() || dirty, false);

      const needsRefresh = (currentNodes.length !== snaphotNodes.length) ||
                           (currentConnections.length !== snaphotConnections.length) ||
                           currentNodesDirty ||
                           currentConnectionsDirty;

      const isDirty = needsRefresh ||
                      (currentName !== snapshot.name) ||
                      (currentDescription !== snapshot.description);

      if (needsRefresh) {
        setTimeout(() => this.refreshCache(), 0);
      }

      return isDirty;
    });
  }

  public override toSnapshot(): WorkflowSnapshot {
    return {
      id: this.id,
      name: this.name(),
      description: this.description(),
      nodes: this.nodes().map(node => node.toSnapshot()),
      connections: this.connections().map(connection => connection.toSnapshot()),
    };
  }

  public static override defaultSnapshot() {
    return {
      ...Entity.defaultSnapshot(),
      name: 'Untitled',
      description: '',
      nodes: [],
      connections: [],
    }
  }

  public static fromSnapshot(snapshot: WorkflowSnapshot): WorkflowEntity {
    return new WorkflowEntity(snapshot);
  }

  public static create(name: string, description: string): WorkflowEntity {
    return new WorkflowEntity({
      ...WorkflowEntity.defaultSnapshot(),
      name: name,
      description: description,
    });
  }

  public override markClean(): void {
    super.markClean();
    this.nodes().forEach(node => node.markClean());
    this.connections().forEach(connection => connection.markClean());
  }

  public getNodeById(id: string): NodeEntity<NodeSnapshot> | undefined {
    return this.nodeMap.get(id);
  }

  public getConnectionById(id: string): ConnectionEntity | undefined {
    return this.connectionMap.get(id);
  }

    public getConnectorById(id: string): ConnectorEntity | undefined {
    return this.connectorMap.get(id);
  }

  private refreshCache(): void {
    this.nodeMap = new Map(this.nodes().map(n => [n.id, n]));
    this.connectionMap = new Map(this.connections().map(c => [c.id, c]));
    this.connectorMap = new Map(this.nodes().flatMap(n => [...n.inputs(), ...n.outputs()]).map(c => [c.id, c]));
  }
}
