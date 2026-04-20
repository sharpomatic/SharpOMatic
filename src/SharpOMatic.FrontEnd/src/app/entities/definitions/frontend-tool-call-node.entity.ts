import { computed, signal, WritableSignal } from '@angular/core';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { NodeType } from '../enumerations/node-type';
import { FrontendToolCallArgumentsMode } from '../enumerations/frontend-tool-call-arguments-mode';
import { FrontendToolCallChatPersistenceMode } from '../enumerations/frontend-tool-call-chat-persistence-mode';

export interface FrontendToolCallNodeSnapshot extends NodeSnapshot {
  functionName: string;
  argumentsMode: FrontendToolCallArgumentsMode;
  argumentsPath: string;
  argumentsJson: string;
  resultOutputPath: string;
  chatPersistenceMode: FrontendToolCallChatPersistenceMode;
  hideFromReplyAfterHandled: boolean;
}

export class FrontendToolCallNodeEntity extends NodeEntity<FrontendToolCallNodeSnapshot> {
  public functionName: WritableSignal<string>;
  public argumentsMode: WritableSignal<FrontendToolCallArgumentsMode>;
  public argumentsPath: WritableSignal<string>;
  public argumentsJson: WritableSignal<string>;
  public resultOutputPath: WritableSignal<string>;
  public chatPersistenceMode: WritableSignal<FrontendToolCallChatPersistenceMode>;
  public hideFromReplyAfterHandled: WritableSignal<boolean>;

  constructor(snapshot: FrontendToolCallNodeSnapshot) {
    super(snapshot);

    this.functionName = signal(snapshot.functionName);
    this.argumentsMode = signal(snapshot.argumentsMode);
    this.argumentsPath = signal(snapshot.argumentsPath);
    this.argumentsJson = signal(snapshot.argumentsJson);
    this.resultOutputPath = signal(snapshot.resultOutputPath);
    this.chatPersistenceMode = signal(snapshot.chatPersistenceMode);
    this.hideFromReplyAfterHandled = signal(snapshot.hideFromReplyAfterHandled);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const original = this.snapshot();

      return (
        isNodeDirty() ||
        this.functionName() !== original.functionName ||
        this.argumentsMode() !== original.argumentsMode ||
        this.argumentsPath() !== original.argumentsPath ||
        this.argumentsJson() !== original.argumentsJson ||
        this.resultOutputPath() !== original.resultOutputPath ||
        this.chatPersistenceMode() !== original.chatPersistenceMode ||
        this.hideFromReplyAfterHandled() !== original.hideFromReplyAfterHandled
      );
    });
  }

  public override toSnapshot(): FrontendToolCallNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      functionName: this.functionName(),
      argumentsMode: this.argumentsMode(),
      argumentsPath: this.argumentsPath(),
      argumentsJson: this.argumentsJson(),
      resultOutputPath: this.resultOutputPath(),
      chatPersistenceMode: this.chatPersistenceMode(),
      hideFromReplyAfterHandled: this.hideFromReplyAfterHandled(),
    };
  }

  public static fromSnapshot(
    snapshot: FrontendToolCallNodeSnapshot,
  ): FrontendToolCallNodeEntity {
    return new FrontendToolCallNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): FrontendToolCallNodeSnapshot {
    const toolResultOutput = ConnectorEntity.defaultSnapshot();
    const otherInputOutput = ConnectorEntity.defaultSnapshot();

    toolResultOutput.name = 'toolResult';
    otherInputOutput.name = 'otherInput';

    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.FrontendToolCall,
      title: 'Frontend Tool Call',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [toolResultOutput, otherInputOutput],
      functionName: '',
      argumentsMode: FrontendToolCallArgumentsMode.FixedJson,
      argumentsPath: '',
      argumentsJson: '{}',
      resultOutputPath: 'output.toolResult',
      chatPersistenceMode:
        FrontendToolCallChatPersistenceMode.FunctionCallAndResult,
      hideFromReplyAfterHandled: false,
    };
  }

  public static create(
    top: number,
    left: number,
  ): FrontendToolCallNodeEntity {
    return new FrontendToolCallNodeEntity({
      ...FrontendToolCallNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
