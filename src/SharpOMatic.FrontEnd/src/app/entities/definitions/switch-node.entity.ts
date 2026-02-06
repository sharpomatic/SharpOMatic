import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';
import { SwitchEntryEntity, SwitchEntrySnapshot } from './switch-entry.entity';

export interface SwitchNodeSnapshot extends NodeSnapshot {
  switches: SwitchEntrySnapshot[];
}

export class SwitchNodeEntity extends NodeEntity<SwitchNodeSnapshot> {
  public switches: WritableSignal<SwitchEntryEntity[]>;

  constructor(snapshot: SwitchNodeSnapshot) {
    super(snapshot);

    this.switches = signal(
      snapshot.switches.map(SwitchEntryEntity.fromSnapshot),
    );

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      // Must touch all property signals
      const currentIsNodeDirty = isNodeDirty();
      const currentSwitchesDirty = this.switches().reduce(
        (dirty, node) => node.isDirty() || dirty,
        false,
      );

      // Copy across the name from switch definition to the connector
      const switches = this.switches();
      const connectors = this.outputs();
      for (var i = 0; i < switches.length; i++) {
        connectors[i].name = switches[i].name;
      }

      return currentIsNodeDirty || currentSwitchesDirty;
    });
  }

  public addSwitch(): void {
    const switches = this.switches();
    const connectors = this.outputs();

    const addSwitch = SwitchEntryEntity.fromSnapshot(
      SwitchEntryEntity.defaultSnapshot(),
    );
    const addConnector = ConnectorEntity.fromSnapshot(
      ConnectorEntity.defaultSnapshot(),
    );

    addConnector.nodeId = this.id;
    addConnector.name.set(addSwitch.name());

    this.switches.update((s) => [
      ...switches.slice(0, -1),
      addSwitch,
      switches[switches.length - 1],
    ]);
    this.outputs.update((o) => [
      ...connectors.slice(0, -1),
      addConnector,
      connectors[connectors.length - 1],
    ]);
  }

  public deleteSwitch(switchId: string): void {
    const switches = this.switches();
    const connectors = this.outputs();

    const index = switches.findIndex((s) => s.id === switchId);

    this.switches.update((s) => [
      ...switches.slice(0, index),
      ...switches.slice(index + 1),
    ]);
    this.outputs.update((o) => [
      ...connectors.slice(0, index),
      ...connectors.slice(index + 1),
    ]);
  }

  public upSwitch(switchId: string): void {
    const switches = this.switches();
    const connectors = this.outputs();

    const index = switches.findIndex((s) => s.id === switchId);

    [switches[index - 1], switches[index]] = [
      switches[index],
      switches[index - 1],
    ];
    [connectors[index - 1], connectors[index]] = [
      connectors[index],
      connectors[index - 1],
    ];

    this.switches.update((s) => [...switches]);
    this.outputs.update((o) => [...connectors]);
  }

  public downSwitch(switchId: string): void {
    const switches = this.switches();
    const connectors = this.outputs();

    const index = switches.findIndex((s) => s.id === switchId);

    [switches[index], switches[index + 1]] = [
      switches[index + 1],
      switches[index],
    ];
    [connectors[index], connectors[index + 1]] = [
      connectors[index + 1],
      connectors[index],
    ];

    this.switches.update((s) => [...switches]);
    this.outputs.update((o) => [...connectors]);
  }

  public override toSnapshot(): SwitchNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      switches: this.switches().map((entry) => entry.toSnapshot()),
    };
  }

  public static fromSnapshot(snapshot: SwitchNodeSnapshot): SwitchNodeEntity {
    return new SwitchNodeEntity(snapshot);
  }

  public static override defaultSnapshot() {
    const matchOutput = ConnectorEntity.defaultSnapshot();
    const matchSwitch = SwitchEntryEntity.defaultSnapshot();

    matchOutput.name = 'match';
    matchSwitch.name = matchOutput.name;

    const defaultOutput = ConnectorEntity.defaultSnapshot();
    const defaultSwitch = SwitchEntryEntity.defaultSnapshot();

    defaultOutput.name = 'default';
    defaultSwitch.name = defaultOutput.name;

    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.Switch,
      title: 'Switch',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [matchOutput, defaultOutput],
      switches: [matchSwitch, defaultSwitch],
    };
  }

  public static create(top: number, left: number): SwitchNodeEntity {
    return new SwitchNodeEntity({
      ...SwitchNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }

  public override markClean(): void {
    super.markClean();
    this.switches().forEach((entry) => entry.markClean());
  }
}
