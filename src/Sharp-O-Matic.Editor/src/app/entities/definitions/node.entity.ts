import { computed, Signal, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity, ConnectorSnapshot } from './connector.entity';
import { Entity, EntitySnapshot } from './entity.entity';
import { NodeStatus } from '../../enumerations/node-status';
import { NodeType } from '../enumerations/node-type';

export interface NodeSnapshot extends EntitySnapshot {
  nodeType: NodeType;
  title: string;
  top: number;
  left: number;
  width: number;
  height: number;
  inputs: ConnectorSnapshot[];
  outputs: ConnectorSnapshot[];
}

export abstract class NodeEntity<T extends NodeSnapshot> extends Entity<T> {
  public nodeType: Signal<NodeType>;
  public title: WritableSignal<string>;
  public top: WritableSignal<number>;
  public left: WritableSignal<number>;
  public width: WritableSignal<number>;
  public height: WritableSignal<number>;
  public inputs: WritableSignal<ConnectorEntity[]>;
  public outputs: WritableSignal<ConnectorEntity[]>;
  public displayState: WritableSignal<NodeStatus>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: T) {
    super(snapshot);

    this.nodeType = signal(snapshot.nodeType);
    this.title = signal(snapshot.title);
    this.top = signal(snapshot.top);
    this.left = signal(snapshot.left);
    this.width = signal(snapshot.width);
    this.height = signal(snapshot.height);
    this.inputs = signal(snapshot.inputs.map(ConnectorEntity.fromSnapshot));
    this.outputs = signal(snapshot.outputs.map(ConnectorEntity.fromSnapshot));
    this.displayState = signal(NodeStatus.None);

    this.pushIdentifiers();
    this.calculateConnectors();

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();
      const snapshotInputs = snapshot.inputs;
      const snapshotOutputs = snapshot.outputs;

      // Must touch all property signals
      const currentTitle = this.title();
      const currentTop = this.top();
      const currentLeft = this.left();
      const currentWidth = this.width();
      const currentHeight = this.height();
      const currentInputs = this.inputs();
      const currentOutputs = this.outputs();

      // Must touch all connector dirty signals
      const currentInputsDirty = currentInputs.reduce((dirty, input) => input.isDirty() || dirty, false);
      const currentOutputssDirty = currentOutputs.reduce((dirty, output) => output.isDirty() || dirty, false);

      const needsRecalc = (currentHeight !== snapshot.height) ||
        (currentInputs.length !== snapshotInputs.length) ||
        (currentOutputs.length !== snapshotOutputs.length) ||
        currentInputsDirty ||
        currentOutputssDirty;

      const isDirty = needsRecalc ||
        (currentTitle !== snapshot.title) ||
        (currentTop !== snapshot.top) ||
        (currentLeft !== snapshot.left) ||
        (currentWidth !== snapshot.width);

      if (needsRecalc) {
        setTimeout(() => {
          this.pushIdentifiers();
          this.calculateConnectors()
        }, 0);
      }

      return isDirty;
    });
  }

  public override markClean(): void {
    super.markClean();
    this.inputs().forEach(connector => connector.markClean());
    this.outputs().forEach(connector => connector.markClean());
  }

  public toNodeSnapshot(): NodeSnapshot {
    return {
      nodeType: this.nodeType(),
      id: this.id,
      title: this.title(),
      top: this.top(),
      left: this.left(),
      width: this.width(),
      height: this.height(),
      inputs: this.inputs().map(connector => connector.toSnapshot()),
      outputs: this.outputs().map(connector => connector.toSnapshot())
    };
  }

  public static override defaultSnapshot(): NodeSnapshot {
    return {
      ...Entity.defaultSnapshot(),
      nodeType: NodeType.Start,
      title: '',
      top: 0,
      left: 0,
      width: 80,
      height: 80,
      inputs: [],
      outputs: []
    }
  }

  private pushIdentifiers(): void {
    this.inputs().forEach(connector => connector.nodeId = this.id);
    this.outputs().forEach(connector => connector.nodeId = this.id);
  }

  private calculateConnectors(): void {
    this.calculateConnectorOffsets(this.inputs());
    this.calculateConnectorOffsets(this.outputs());
  }

  private calculateConnectorOffsets(connectors: ConnectorEntity[]): void {
    const allocateHeight = ConnectorEntity.DISPLAY_SIZE * 2;
    const allocateOffset = ConnectorEntity.DISPLAY_SIZE / 2;

    if (connectors.length > 0) {
      const minHeight = (connectors.length + 1) * allocateHeight;
      if (this.height() < minHeight) {
        this.height.set(minHeight);
        this.calculateConnectors();
        return;
      }

      const topOffset = (this.height() - minHeight + allocateHeight) / 2 - 3;
      connectors.forEach((output, index) => {
        output.boxOffset.set(topOffset + allocateOffset + (index * allocateHeight) - 1);
        output.labelOffsetV.set(output.boxOffset() - 7);
        output.labelOffsetH.set(this.width() + ConnectorEntity.DISPLAY_SIZE);
      });
    }
  }
}
