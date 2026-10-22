import { computed, Signal, signal, WritableSignal } from '@angular/core';
import { Entity, EntitySnapshot } from './entity.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { ConnectionEntity, ConnectionSnapshot } from './connection.entity';
import { nodeFromSnapshot } from './node-factory';
import { ConnectorEntity } from './connector.entity';

export interface WorkflowSnapshot extends EntitySnapshot {
  workflowFolderId?: string | null;
  workflowFolderName?: string | null;
  name: string;
  description: string;
  isConversationEnabled: boolean;
  tenantIdPath?: string | null;
  userIdPath?: string | null;
  referenceIdPath?: string | null;
  referenceTypePath?: string | null;
  nodes: NodeSnapshot[];
  connections: ConnectionSnapshot[];
}

export class WorkflowEntity extends Entity<WorkflowSnapshot> {
  public static readonly CURRENT_SCHEMA_VERSION = 2;
  public static readonly DEFAULT_TENANT_ID_PATH = 'auth.tenantId';
  public static readonly DEFAULT_USER_ID_PATH = 'auth.userId';
  private nodeMap: Map<string, NodeEntity<NodeSnapshot>> = new Map();
  private connectionMap: Map<string, ConnectionEntity> = new Map();
  private connectorMap: Map<string, ConnectorEntity> = new Map();

  public name: WritableSignal<string>;
  public workflowFolderId: WritableSignal<string | null>;
  public workflowFolderName: WritableSignal<string | null>;
  public description: WritableSignal<string>;
  public isConversationEnabled: WritableSignal<boolean>;
  public tenantIdPath: WritableSignal<string>;
  public userIdPath: WritableSignal<string>;
  public referenceIdPath: WritableSignal<string>;
  public referenceTypePath: WritableSignal<string>;
  public nodes: WritableSignal<NodeEntity<NodeSnapshot>[]>;
  public connections: WritableSignal<ConnectionEntity[]>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: WorkflowSnapshot) {
    super(snapshot);

    this.workflowFolderId = signal(snapshot.workflowFolderId ?? null);
    this.workflowFolderName = signal(snapshot.workflowFolderName ?? null);
    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.isConversationEnabled = signal(snapshot.isConversationEnabled);
    this.tenantIdPath = signal(
      snapshot.tenantIdPath ?? WorkflowEntity.DEFAULT_TENANT_ID_PATH,
    );
    this.userIdPath = signal(
      snapshot.userIdPath ?? WorkflowEntity.DEFAULT_USER_ID_PATH,
    );
    this.referenceIdPath = signal(snapshot.referenceIdPath ?? '');
    this.referenceTypePath = signal(snapshot.referenceTypePath ?? '');
    this.nodes = signal(snapshot.nodes.map(nodeFromSnapshot));
    this.connections = signal(
      snapshot.connections.map(ConnectionEntity.fromSnapshot),
    );

    this.refreshCache();

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();
      const snaphotNodes = snapshot.nodes;
      const snaphotConnections = snapshot.connections;

      // Must touch all property signals
      const currentName = this.name();
      const currentWorkflowFolderId = this.workflowFolderId();
      const currentDescription = this.description();
      const currentIsConversationEnabled = this.isConversationEnabled();
      const currentTenantIdPath = this.tenantIdPath();
      const currentUserIdPath = this.userIdPath();
      const currentReferenceIdPath = this.referenceIdPath();
      const currentReferenceTypePath = this.referenceTypePath();
      const currentNodes = this.nodes();
      const currentConnections = this.connections();

      // Must touch all nodes/connections dirty signals
      const currentNodesDirty = currentNodes.reduce(
        (dirty, node) => node.isDirty() || dirty,
        false,
      );
      const currentConnectionsDirty = currentConnections.reduce(
        (dirty, connection) => connection.isDirty() || dirty,
        false,
      );
      const connectionIdsChanged = this.haveConnectionIdsChanged(
        currentConnections,
        snaphotConnections,
      );

      const needsRefresh =
        currentNodes.length !== snaphotNodes.length ||
        currentConnections.length !== snaphotConnections.length ||
        connectionIdsChanged ||
        currentNodesDirty ||
        currentConnectionsDirty;

      const isDirty =
        needsRefresh ||
        currentWorkflowFolderId !== (snapshot.workflowFolderId ?? null) ||
        currentName !== snapshot.name ||
        currentDescription !== snapshot.description ||
        currentIsConversationEnabled !== snapshot.isConversationEnabled ||
        currentTenantIdPath !==
          (snapshot.tenantIdPath ?? WorkflowEntity.DEFAULT_TENANT_ID_PATH) ||
        currentUserIdPath !==
          (snapshot.userIdPath ?? WorkflowEntity.DEFAULT_USER_ID_PATH) ||
        currentReferenceIdPath !== (snapshot.referenceIdPath ?? '') ||
        currentReferenceTypePath !== (snapshot.referenceTypePath ?? '');

      if (needsRefresh) {
        setTimeout(() => this.refreshCache(), 0);
      }

      return isDirty;
    });
  }

  public override toSnapshot(): WorkflowSnapshot {
    return {
      id: this.id,
      version: this.version,
      workflowFolderId: this.workflowFolderId(),
      workflowFolderName: this.workflowFolderName(),
      name: this.name(),
      description: this.description(),
      isConversationEnabled: this.isConversationEnabled(),
      tenantIdPath: this.tenantIdPath(),
      userIdPath: this.userIdPath(),
      referenceIdPath: this.referenceIdPath(),
      referenceTypePath: this.referenceTypePath(),
      nodes: this.nodes().map((node) => node.toSnapshot()),
      connections: this.connections().map((connection) =>
        connection.toSnapshot(),
      ),
    };
  }

  public static override defaultSnapshot() {
    return {
      ...Entity.defaultSnapshot(),
      version: WorkflowEntity.CURRENT_SCHEMA_VERSION,
      workflowFolderId: null,
      workflowFolderName: null,
      name: 'Untitled',
      description: '',
      isConversationEnabled: false,
      tenantIdPath: WorkflowEntity.DEFAULT_TENANT_ID_PATH,
      userIdPath: WorkflowEntity.DEFAULT_USER_ID_PATH,
      referenceIdPath: '',
      referenceTypePath: '',
      nodes: [],
      connections: [],
    };
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
    this.nodes().forEach((node) => node.markClean());
    this.connections().forEach((connection) => connection.markClean());
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

  private haveConnectionIdsChanged(
    currentConnections: ConnectionEntity[],
    snapshotConnections: ConnectionSnapshot[],
  ): boolean {
    if (currentConnections.length !== snapshotConnections.length) {
      return true;
    }

    const currentIds = new Set(currentConnections.map((c) => c.id));
    const snapshotIds = new Set(snapshotConnections.map((c) => c.id));

    if (currentIds.size !== snapshotIds.size) {
      return true;
    }

    for (const id of currentIds) {
      if (!snapshotIds.has(id)) {
        return true;
      }
    }

    return false;
  }

  private refreshCache(): void {
    this.nodeMap = new Map(this.nodes().map((n) => [n.id, n]));
    this.connectionMap = new Map(this.connections().map((c) => [c.id, c]));
    this.connectorMap = new Map(
      this.nodes()
        .flatMap((n) => [...n.inputs(), ...n.outputs()])
        .map((c) => [c.id, c]),
    );
  }
}
