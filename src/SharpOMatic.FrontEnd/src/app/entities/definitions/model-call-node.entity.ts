import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';

export interface ModelCallNodeSnapshot extends NodeSnapshot {
  modelId: string | null;
  instructions: string;
  prompt: string;
  chatInputPath: string;
  chatOutputPath: string;
  textOutputPath: string;
  imageInputPath: string;
  imageOutputPath: string;
  parameterValues: Record<string, string | null>;
}

export class ModelCallNodeEntity extends NodeEntity<ModelCallNodeSnapshot> {
  public modelId: WritableSignal<string | null>;
  public instructions: WritableSignal<string>;
  public prompt: WritableSignal<string>;
  public chatInputPath: WritableSignal<string>;
  public chatOutputPath: WritableSignal<string>;
  public textOutputPath: WritableSignal<string>;
  public imageInputPath: WritableSignal<string>;
  public imageOutputPath: WritableSignal<string>;
  public parameterValues: WritableSignal<Record<string, string | null>>;

  constructor(snapshot: ModelCallNodeSnapshot) {
    super(snapshot);

    this.modelId = signal(snapshot.modelId);
    this.instructions = signal(snapshot.instructions ?? '');
    this.prompt = signal(snapshot.prompt ?? '');
    this.chatInputPath = signal(snapshot.chatInputPath ?? '');
    this.chatOutputPath = signal(snapshot.chatOutputPath ?? '');
    this.textOutputPath = signal(snapshot.textOutputPath ?? '');
    this.imageInputPath = signal(snapshot.imageInputPath ?? '');
    this.imageOutputPath = signal(snapshot.imageOutputPath ?? '');
    this.parameterValues = signal({ ...(snapshot.parameterValues ?? {}) });

    const baseIsDirty = this.isDirty;
    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentIsDirty = baseIsDirty();
      const currentModelId = this.modelId();
      const currentInstructions = this.instructions();
      const currentPrompt = this.prompt();
      const currentChatInputPath = this.chatInputPath();
      const currentChatOutputPath = this.chatOutputPath();
      const currentTextOutputPath = this.textOutputPath();
      const currentImageInputPath = this.imageInputPath();
      const currentImageOutputPath = this.imageOutputPath();
      const currentParameterValues = this.parameterValues();

      return (
        currentIsDirty ||
        currentModelId !== snapshot.modelId ||
        currentInstructions !== snapshot.instructions ||
        currentPrompt !== snapshot.prompt ||
        currentChatInputPath !== (snapshot.chatInputPath ?? '') ||
        currentChatOutputPath !== (snapshot.chatOutputPath ?? '') ||
        currentTextOutputPath !== snapshot.textOutputPath ||
        currentImageInputPath !== snapshot.imageInputPath ||
        currentImageOutputPath !== snapshot.imageOutputPath ||
        !ModelCallNodeEntity.areParameterValuesEqual(
          currentParameterValues,
          snapshot.parameterValues,
        )
      );
    });
  }

  public override toSnapshot(): ModelCallNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      modelId: this.modelId(),
      instructions: this.instructions(),
      prompt: this.prompt(),
      chatInputPath: this.chatInputPath(),
      chatOutputPath: this.chatOutputPath(),
      textOutputPath: this.textOutputPath(),
      imageInputPath: this.imageInputPath(),
      imageOutputPath: this.imageOutputPath(),
      parameterValues: this.parameterValues(),
    };
  }

  public static fromSnapshot(
    snapshot: ModelCallNodeSnapshot,
  ): ModelCallNodeEntity {
    return new ModelCallNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): ModelCallNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.ModelCall,
      title: 'Model Call',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      modelId: null,
      instructions: '',
      prompt: '',
      chatInputPath: '',
      chatOutputPath: '',
      textOutputPath: 'output.text',
      imageInputPath: '',
      imageOutputPath: 'output.image',
      parameterValues: {},
    };
  }

  public static create(top: number, left: number): ModelCallNodeEntity {
    return new ModelCallNodeEntity({
      ...ModelCallNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }

  private static areParameterValuesEqual(
    current: Record<string, string | null>,
    snapshot: Record<string, string | null>,
  ): boolean {
    const currentEntries = Object.entries(current ?? {});
    const snapshotEntries = Object.entries(snapshot ?? {});

    if (currentEntries.length !== snapshotEntries.length) {
      return false;
    }

    return currentEntries.every(
      ([key, value]) => (snapshot ?? {})[key] === value,
    );
  }
}
