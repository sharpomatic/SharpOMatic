import { computed, signal, WritableSignal } from '@angular/core';
import { StreamMessageRole } from '../../enumerations/stream-message-role';
import { ConnectorEntity } from './connector.entity';
import { NodeEntity, NodeSnapshot } from './node.entity';
import { EventTemplateOutputMode } from '../enumerations/event-template-output-mode';
import { NodeType } from '../enumerations/node-type';

export interface EventTemplateNodeSnapshot extends NodeSnapshot {
  template: string;
  outputMode: EventTemplateOutputMode;
  textRole: StreamMessageRole;
  silent: boolean;
}

export class EventTemplateNodeEntity extends NodeEntity<EventTemplateNodeSnapshot> {
  public template: WritableSignal<string>;
  public outputMode: WritableSignal<EventTemplateOutputMode>;
  public textRole: WritableSignal<StreamMessageRole>;
  public silent: WritableSignal<boolean>;

  constructor(snapshot: EventTemplateNodeSnapshot) {
    super(snapshot);

    this.template = signal(snapshot.template ?? '');
    this.outputMode = signal(snapshot.outputMode ?? EventTemplateOutputMode.Text);
    this.textRole = signal(snapshot.textRole ?? StreamMessageRole.Assistant);
    this.silent = signal(snapshot.silent ?? false);

    const isNodeDirty = this.isDirty;
    this.isDirty = computed(() => {
      const original = this.snapshot();

      return (
        isNodeDirty() ||
        this.template() !== (original.template ?? '') ||
        this.outputMode() !==
          (original.outputMode ?? EventTemplateOutputMode.Text) ||
        this.textRole() !== (original.textRole ?? StreamMessageRole.Assistant) ||
        this.silent() !== (original.silent ?? false)
      );
    });
  }

  public override toSnapshot(): EventTemplateNodeSnapshot {
    return {
      ...super.toNodeSnapshot(),
      template: this.template(),
      outputMode: this.outputMode(),
      textRole: this.textRole(),
      silent: this.silent(),
    };
  }

  public static fromSnapshot(
    snapshot: EventTemplateNodeSnapshot,
  ): EventTemplateNodeEntity {
    return new EventTemplateNodeEntity(snapshot);
  }

  public static override defaultSnapshot(): EventTemplateNodeSnapshot {
    return {
      ...NodeEntity.defaultSnapshot(),
      nodeType: NodeType.EventTemplate,
      title: 'Event Template',
      inputs: [ConnectorEntity.defaultSnapshot()],
      outputs: [ConnectorEntity.defaultSnapshot()],
      template: '',
      outputMode: EventTemplateOutputMode.Text,
      textRole: StreamMessageRole.Assistant,
      silent: false,
    };
  }

  public static create(top: number, left: number): EventTemplateNodeEntity {
    return new EventTemplateNodeEntity({
      ...EventTemplateNodeEntity.defaultSnapshot(),
      top,
      left,
    });
  }
}
