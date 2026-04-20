import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';
import { ToolCallDataMode } from '../enumerations/tool-call-data-mode';
import { ToolCallChatPersistenceMode } from '../enumerations/tool-call-chat-persistence-mode';

export interface BackendToolCallNodeSnapshot extends NodeSnapshot {
  functionName: string;
  argumentsMode: ToolCallDataMode;
  argumentsPath: string;
  argumentsJson: string;
  resultMode: ToolCallDataMode;
  resultPath: string;
  resultJson: string;
  chatPersistenceMode: ToolCallChatPersistenceMode;
}

export class BackendToolCallNodeEntity extends NodeEntity<BackendToolCallNodeSnapshot> {
  public functionName: WritableSignal<string>;
  public argumentsMode: WritableSignal<ToolCallDataMode>;
  public argumentsPath: WritableSignal<string>;
  public argumentsJson: WritableSignal<string>;
  public resultMode: WritableSignal<ToolCallDataMode>;
  public resultPath: WritableSignal<string>;
  public resultJson: WritableSignal<string>;
  public chatPersistenceMode: WritableSignal<ToolCallChatPersistenceMode>;

  constructor(snapshot: BackendToolCallNodeSnapshot) {
    super(snapshot);

    this.functionName = signal(snapshot.functionName);
    this.argumentsMode = signal(snapshot.argumentsMode);
    this.argumentsPath = signal(snapshot.argumentsPath);
    this.argumentsJson = signal(snapshot.argumentsJson);
    this.resultMode = signal(snapshot.resultMode);
    this.resultPath = signal(snapshot.resultPath);
    this.resultJson = signal(snapshot.resultJson);
    this.chatPersistenceMode = signal(snapshot.chatPersistenceMode);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const original = this.snapshot();

      return (
        isNodeDirty() ||
        this.functionName() !== original.functionName ||
        this.argumentsMode() !== original.argumentsMode ||
        this.argumentsPath() !== original.argumentsPath ||
        this.argumentsJson() !== original.argumentsJson ||
        this.resultMode() !== original.resultMode ||
        this.resultPath() !== original.resultPath ||
        this.resultJson() !== original.resultJson ||
        this.chatPersistenceMode() !== original.chatPersistenceMode
      );
    });
  }

  public override toSnapshot(): BackendToolCallNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      functionName: this.functionName(),
      argumentsMode: this.argumentsMode(),
      argumentsPath: this.argumentsPath(),
      argumentsJson: this.argumentsJson(),
      resultMode: this.resultMode(),
      resultPath: this.resultPath(),
      resultJson: this.resultJson(),
      chatPersistenceMode: this.chatPersistenceMode(),
    };
  }

  public static fromSnapshot(
    snapshot: BackendToolCallNodeSnapshot,
  ): BackendToolCallNodeEntity {
    return new BackendToolCallNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): BackendToolCallNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.BackendToolCall,
      title: 'BE Tool Call',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      functionName: '',
      argumentsMode: ToolCallDataMode.FixedJson,
      argumentsPath: '',
      argumentsJson: '{}',
      resultMode: ToolCallDataMode.FixedJson,
      resultPath: '',
      resultJson: '{}',
      chatPersistenceMode: ToolCallChatPersistenceMode.FunctionCallAndResult,
    };
  }

  public static create(
    top: number,
    left: number,
  ): BackendToolCallNodeEntity {
    return new BackendToolCallNodeEntity({
      ...BackendToolCallNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
