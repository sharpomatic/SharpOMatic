import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface FanOutNodeSnapshot extends NodeSnapshot {
  names: string[];
}

export class FanOutNodeEntity extends NodeEntity<FanOutNodeSnapshot> {
  public names: WritableSignal<string[]>;

  constructor(snapshot: FanOutNodeSnapshot) {
    super(snapshot);

    this.names = signal([...snapshot.names]);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const snapshotNames = this.snapshot().names;
      const currentNames = this.names();

      this.syncConnectorNames(currentNames);

      const namesLengthChanged = currentNames.length !== snapshotNames.length;
      const namesContentChanged =
        namesLengthChanged ||
        currentNames.some((name, index) => name !== snapshotNames[index]);

      return isNodeDirty() || namesContentChanged;
    });

    this.syncConnectorNames(this.names());
  }

  public addOutput(name: string): void {
    const newConnector = ConnectorEntity.fromSnapshot(
      ConnectorEntity.defaultSnapshot(),
    );
    newConnector.nodeId = this.id;
    newConnector.name.set(name);

    this.names.update((n) => [...n, name]);
    this.outputs.update((o) => [...o, newConnector]);
  }

  public deleteOutput(index: number): void {
    this.names.update((n) => [...n.slice(0, index), ...n.slice(index + 1)]);
    this.outputs.update((o) => [...o.slice(0, index), ...o.slice(index + 1)]);
  }

  public updateOutput(index: number, name: string): void {
    this.names.update((n) => {
      if (index < 0 || index >= n.length) {
        return n;
      }

      const updated = [...n];
      updated[index] = name;
      return updated;
    });

    this.outputs.update((o) => {
      if (index < 0 || index >= o.length) {
        return o;
      }

      const updated = [...o];
      updated[index].nodeId = this.id;
      updated[index].name.set(name);
      return updated;
    });
  }

  public override toSnapshot(): FanOutNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      names: this.names(),
    };
  }

  public static fromSnapshot(snapshot: FanOutNodeSnapshot): FanOutNodeEntity {
    return new FanOutNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): FanOutNodeSnapshot {
    const firstOutput = ConnectorEntity.defaultSnapshot();
    const secondOutput = ConnectorEntity.defaultSnapshot();
    const names = [firstOutput.name, secondOutput.name];

    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.FanOut,
      title: 'Fan Out',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [firstOutput, secondOutput],
      names,
    };
  }

  public static create(top: number, left: number): FanOutNodeEntity {
    return new FanOutNodeEntity({
      ...FanOutNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }

  private syncConnectorNames(names: string[]): void {
    const connectors = this.outputs();
    connectors.forEach((connector, index) => {
      connector.nodeId = this.id;
      const targetName = names[index] ?? '';
      if (connector.name() !== targetName) {
        connector.name.set(targetName);
      }
    });
  }
}
