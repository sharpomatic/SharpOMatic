import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';
import { ModelCallToolAgUiOutputMode } from '../enumerations/model-call-tool-ag-ui-output-mode';

export interface ModelCallNodeSnapshot extends NodeSnapshot {
  models: ModelCallModelSnapshot[];
  batchOutput?: boolean;
  dropToolCalls?: boolean;
  disableStreamUser?: boolean;
  disableStreamTool?: boolean;
  disableStreamReasoning?: boolean;
  disableStreamAssistantText?: boolean;
  instructions: string;
  prompt: string;
  chatInputPath: string;
  chatOutputPath: string;
  textOutputPath: string;
  imageInputPath: string;
  imageOutputPath: string;
  toolAgUiOutputModes?: Record<string, ModelCallToolAgUiOutputMode>;
}

export interface ModelCallModelSnapshot {
  modelId: string;
  disabled?: boolean;
  parameterValues: Record<string, string | null>;
}

export class ModelCallNodeEntity extends NodeEntity<ModelCallNodeSnapshot> {
  public models: WritableSignal<ModelCallModelSnapshot[]>;
  // Primary-model editing signals retained so existing editor bindings stay simple.
  public modelId: WritableSignal<string | null>;
  public batchOutput: WritableSignal<boolean>;
  public dropToolCalls: WritableSignal<boolean>;
  public disableStreamUser: WritableSignal<boolean>;
  public disableStreamTool: WritableSignal<boolean>;
  public disableStreamReasoning: WritableSignal<boolean>;
  public disableStreamAssistantText: WritableSignal<boolean>;
  public instructions: WritableSignal<string>;
  public prompt: WritableSignal<string>;
  public chatInputPath: WritableSignal<string>;
  public chatOutputPath: WritableSignal<string>;
  public textOutputPath: WritableSignal<string>;
  public imageInputPath: WritableSignal<string>;
  public imageOutputPath: WritableSignal<string>;
  public parameterValues: WritableSignal<Record<string, string | null>>;
  public toolAgUiOutputModes: WritableSignal<
    Record<string, ModelCallToolAgUiOutputMode>
  >;

  constructor(snapshot: ModelCallNodeSnapshot) {
    super(snapshot);

    const initialModels = (snapshot.models ?? []).map((model) =>
      ModelCallNodeEntity.cloneModel(model),
    );
    this.models = signal(initialModels);
    this.modelId = signal(initialModels[0]?.modelId ?? null);
    this.batchOutput = signal(snapshot.batchOutput ?? false);
    this.dropToolCalls = signal(snapshot.dropToolCalls ?? false);
    this.disableStreamUser = signal(snapshot.disableStreamUser ?? false);
    this.disableStreamTool = signal(snapshot.disableStreamTool ?? false);
    this.disableStreamReasoning = signal(
      snapshot.disableStreamReasoning ?? false,
    );
    this.disableStreamAssistantText = signal(
      snapshot.disableStreamAssistantText ?? false,
    );
    this.instructions = signal(snapshot.instructions ?? '');
    this.prompt = signal(snapshot.prompt ?? '');
    this.chatInputPath = signal(snapshot.chatInputPath ?? '');
    this.chatOutputPath = signal(snapshot.chatOutputPath ?? '');
    this.textOutputPath = signal(snapshot.textOutputPath ?? '');
    this.imageInputPath = signal(snapshot.imageInputPath ?? '');
    this.imageOutputPath = signal(snapshot.imageOutputPath ?? '');
    this.parameterValues = signal({
      ...(initialModels[0]?.parameterValues ?? {}),
    });
    this.toolAgUiOutputModes = signal({
      ...(snapshot.toolAgUiOutputModes ?? {}),
    });

    const baseIsDirty = this.isDirty;
    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentIsDirty = baseIsDirty();
      const currentModels = this.effectiveModels();
      const currentBatchOutput = this.batchOutput();
      const currentDropToolCalls = this.dropToolCalls();
      const currentDisableStreamUser = this.disableStreamUser();
      const currentDisableStreamTool = this.disableStreamTool();
      const currentDisableStreamReasoning = this.disableStreamReasoning();
      const currentDisableStreamAssistantText =
        this.disableStreamAssistantText();
      const currentInstructions = this.instructions();
      const currentPrompt = this.prompt();
      const currentChatInputPath = this.chatInputPath();
      const currentChatOutputPath = this.chatOutputPath();
      const currentTextOutputPath = this.textOutputPath();
      const currentImageInputPath = this.imageInputPath();
      const currentImageOutputPath = this.imageOutputPath();
      const currentToolAgUiOutputModes = this.toolAgUiOutputModes();

      return (
        currentIsDirty ||
        !ModelCallNodeEntity.areModelsEqual(
          currentModels,
          snapshot.models ?? [],
        ) ||
        currentBatchOutput !== (snapshot.batchOutput ?? false) ||
        currentDropToolCalls !== (snapshot.dropToolCalls ?? false) ||
        currentDisableStreamUser !== (snapshot.disableStreamUser ?? false) ||
        currentDisableStreamTool !== (snapshot.disableStreamTool ?? false) ||
        currentDisableStreamReasoning !==
          (snapshot.disableStreamReasoning ?? false) ||
        currentDisableStreamAssistantText !==
          (snapshot.disableStreamAssistantText ?? false) ||
        currentInstructions !== snapshot.instructions ||
        currentPrompt !== snapshot.prompt ||
        currentChatInputPath !== (snapshot.chatInputPath ?? '') ||
        currentChatOutputPath !== (snapshot.chatOutputPath ?? '') ||
        currentTextOutputPath !== snapshot.textOutputPath ||
        currentImageInputPath !== snapshot.imageInputPath ||
        currentImageOutputPath !== snapshot.imageOutputPath ||
        !ModelCallNodeEntity.areRecordsEqual(
          currentToolAgUiOutputModes,
          snapshot.toolAgUiOutputModes ?? {},
        )
      );
    });
  }

  public override toSnapshot(): ModelCallNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      models: this.effectiveModels(),
      batchOutput: this.batchOutput(),
      dropToolCalls: this.dropToolCalls(),
      disableStreamUser: this.disableStreamUser(),
      disableStreamTool: this.disableStreamTool(),
      disableStreamReasoning: this.disableStreamReasoning(),
      disableStreamAssistantText: this.disableStreamAssistantText(),
      instructions: this.instructions(),
      prompt: this.prompt(),
      chatInputPath: this.chatInputPath(),
      chatOutputPath: this.chatOutputPath(),
      textOutputPath: this.textOutputPath(),
      imageInputPath: this.imageInputPath(),
      imageOutputPath: this.imageOutputPath(),
      toolAgUiOutputModes: this.toolAgUiOutputModes(),
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
      models: [],
      batchOutput: false,
      dropToolCalls: false,
      disableStreamUser: false,
      disableStreamTool: false,
      disableStreamReasoning: false,
      disableStreamAssistantText: false,
      instructions: '',
      prompt: '',
      chatInputPath: '',
      chatOutputPath: '',
      textOutputPath: 'output.text',
      imageInputPath: '',
      imageOutputPath: 'output.image',
      toolAgUiOutputModes: {},
    };
  }

  public static create(top: number, left: number): ModelCallNodeEntity {
    return new ModelCallNodeEntity({
      ...ModelCallNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }

  public setFallbackModels(models: ModelCallModelSnapshot[]): void {
    const primary = this.effectiveModels()[0];
    this.models.set(primary ? [primary, ...models] : [...models]);
  }

  public fallbackModels(): ModelCallModelSnapshot[] {
    return this.effectiveModels().slice(1);
  }

  public modelDisabled(index: number): boolean {
    return this.effectiveModels()[index]?.disabled ?? false;
  }

  public setModelDisabled(index: number, disabled: boolean): void {
    const models = this.effectiveModels();
    const model = models[index];
    if (!model) {
      return;
    }

    models[index] = ModelCallNodeEntity.cloneModel({ ...model, disabled });
    this.models.set(models);
  }

  private effectiveModels(): ModelCallModelSnapshot[] {
    const modelId = this.modelId();
    const fallbacks = this.models()
      .slice(1)
      .map((model) => ModelCallNodeEntity.cloneModel(model));

    if (!modelId) {
      return [];
    }

    return [
      ModelCallNodeEntity.cloneModel({
        modelId,
        disabled: this.models()[0]?.disabled ?? false,
        parameterValues: { ...this.parameterValues() },
      }),
      ...fallbacks,
    ];
  }

  private static cloneModel(
    model: ModelCallModelSnapshot,
  ): ModelCallModelSnapshot {
    return {
      modelId: model.modelId,
      ...(model.disabled ? { disabled: true } : {}),
      parameterValues: { ...(model.parameterValues ?? {}) },
    };
  }

  private static areModelsEqual(
    current: ModelCallModelSnapshot[],
    snapshot: ModelCallModelSnapshot[],
  ): boolean {
    return (
      current.length === snapshot.length &&
      current.every(
        (model, index) =>
          model.modelId === snapshot[index]?.modelId &&
          (model.disabled ?? false) === (snapshot[index]?.disabled ?? false) &&
          ModelCallNodeEntity.areRecordsEqual(
            model.parameterValues,
            snapshot[index]?.parameterValues ?? {},
          ),
      )
    );
  }

  private static areRecordsEqual<T>(
    current: Record<string, T>,
    snapshot: Record<string, T>,
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
