import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface BatchNodeSnapshot extends NodeSnapshot {
  inputArrayPath: string;
  outputArrayPath: string;
  batchSize: number;
  parallelBatches: number;
}

export class BatchNodeEntity extends NodeEntity<BatchNodeSnapshot> {
  public inputArrayPath: WritableSignal<string>;
  public outputArrayPath: WritableSignal<string>;
  public batchSize: WritableSignal<number>;
  public parallelBatches: WritableSignal<number>;

  constructor(snapshot: BatchNodeSnapshot) {
    super(snapshot);

    this.inputArrayPath = signal(snapshot.inputArrayPath);
    this.outputArrayPath = signal(snapshot.outputArrayPath);
    this.batchSize = signal(snapshot.batchSize);
    this.parallelBatches = signal(snapshot.parallelBatches);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      const currentIsNodeDirty = isNodeDirty();
      const currentInputArrayPath = this.inputArrayPath();
      const currentOutputArrayPath = this.outputArrayPath();
      const currentBatchSize = this.batchSize();
      const currentParallelBatches = this.parallelBatches();

      return (
        currentIsNodeDirty ||
        currentInputArrayPath !== snapshot.inputArrayPath ||
        currentOutputArrayPath !== snapshot.outputArrayPath ||
        currentBatchSize !== snapshot.batchSize ||
        currentParallelBatches !== snapshot.parallelBatches
      );
    });
  }

  public override toSnapshot(): BatchNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      inputArrayPath: this.inputArrayPath(),
      outputArrayPath: this.outputArrayPath(),
      batchSize: this.batchSize(),
      parallelBatches: this.parallelBatches(),
    };
  }

  public static fromSnapshot(snapshot: BatchNodeSnapshot): BatchNodeEntity {
    return new BatchNodeEntity(snapshot);
  }

  public static override defaultSnapshot() {
    const continueOutput = ConnectorEntity.defaultSnapshot();
    const processOutput = ConnectorEntity.defaultSnapshot();

    continueOutput.name = 'continue';
    processOutput.name = 'process';

    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.Batch,
      title: 'Batch',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [continueOutput, processOutput],
      inputArrayPath: '',
      outputArrayPath: 'output',
      batchSize: 10,
      parallelBatches: 3,
    };
  }

  public static create(top: number, left: number): BatchNodeEntity {
    return new BatchNodeEntity({
      ...BatchNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
