import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { StreamEventKind } from '../../enumerations/stream-event-kind';
import { StreamMessageRole } from '../../enumerations/stream-message-role';
import { StreamEventModel } from '../../pages/workflow/interfaces/stream-event-model';

@Component({
  selector: 'app-stream-viewer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './stream-viewer.component.html',
  styleUrls: ['./stream-viewer.component.scss'],
})
export class StreamViewerComponent {
  private textRoleByMessageId = new Map<string, StreamMessageRole>();

  @Input()
  set streamEvents(value: StreamEventModel[]) {
    this._streamEvents = value ?? [];
    this.textRoleByMessageId = this.buildTextRoleLookup(this._streamEvents);
  }

  get streamEvents(): StreamEventModel[] {
    return this._streamEvents;
  }

  private _streamEvents: StreamEventModel[] = [];

  public readonly StreamEventKind = StreamEventKind;
  public readonly StreamMessageRole = StreamMessageRole;

  public getTextEventRole(streamEvent: StreamEventModel): StreamMessageRole | null {
    if (!this.isTextEvent(streamEvent) || !streamEvent.messageId) {
      return null;
    }

    return this.textRoleByMessageId.get(streamEvent.messageId) ?? null;
  }

  public getEventKindLabel(kind: StreamEventKind): string {
    switch (kind) {
      case StreamEventKind.TextStart:
        return 'Text Start';
      case StreamEventKind.TextContent:
        return 'Text Content';
      case StreamEventKind.TextEnd:
        return 'Text End';
      case StreamEventKind.ReasoningStart:
        return 'Reasoning Start';
      case StreamEventKind.ReasoningMessageStart:
        return 'Reasoning Msg Start';
      case StreamEventKind.ReasoningMessageContent:
        return 'Reasoning Msg Content';
      case StreamEventKind.ReasoningMessageEnd:
        return 'Reasoning Msg End';
      case StreamEventKind.ReasoningEnd:
        return 'Reasoning End';
      case StreamEventKind.ToolCallStart:
        return 'Tool Call Start';
      case StreamEventKind.ToolCallArgs:
        return 'Tool Call Args';
      case StreamEventKind.ToolCallEnd:
        return 'Tool Call End';
      case StreamEventKind.ToolCallResult:
        return 'Tool Call Result';
      case StreamEventKind.ActivitySnapshot:
        return 'Activity Snapshot';
      case StreamEventKind.ActivityDelta:
        return 'Activity Delta';
      case StreamEventKind.StepStart:
        return 'Step Start';
      case StreamEventKind.StepEnd:
        return 'Step End';
      case StreamEventKind.StateSnapshot:
        return 'State Snapshot';
      case StreamEventKind.StateDelta:
        return 'State Delta';
      default:
        return 'Unknown';
    }
  }

  public isStepEvent(streamEvent: StreamEventModel): boolean {
    return (
      streamEvent.eventKind === StreamEventKind.StepStart ||
      streamEvent.eventKind === StreamEventKind.StepEnd
    );
  }

  public isActivitySnapshotEvent(streamEvent: StreamEventModel): boolean {
    return streamEvent.eventKind === StreamEventKind.ActivitySnapshot;
  }

  public isActivityDeltaEvent(streamEvent: StreamEventModel): boolean {
    return streamEvent.eventKind === StreamEventKind.ActivityDelta;
  }

  public isStateSnapshotEvent(streamEvent: StreamEventModel): boolean {
    return streamEvent.eventKind === StreamEventKind.StateSnapshot;
  }

  public isStateDeltaEvent(streamEvent: StreamEventModel): boolean {
    return streamEvent.eventKind === StreamEventKind.StateDelta;
  }

  public isAssistantTextEvent(streamEvent: StreamEventModel): boolean {
    return this.getTextEventRole(streamEvent) === StreamMessageRole.Assistant;
  }

  public isUserTextEvent(streamEvent: StreamEventModel): boolean {
    return this.getTextEventRole(streamEvent) === StreamMessageRole.User;
  }

  public isSystemTextEvent(streamEvent: StreamEventModel): boolean {
    return this.getTextEventRole(streamEvent) === StreamMessageRole.System;
  }

  public isDeveloperTextEvent(streamEvent: StreamEventModel): boolean {
    return this.getTextEventRole(streamEvent) === StreamMessageRole.Developer;
  }

  public isToolTextEvent(streamEvent: StreamEventModel): boolean {
    return this.getTextEventRole(streamEvent) === StreamMessageRole.Tool;
  }

  public getRoleLabel(role?: StreamMessageRole | null): string {
    if (role === null || role === undefined) {
      return '';
    }

    switch (role) {
      case StreamMessageRole.User:
        return 'User';
      case StreamMessageRole.Assistant:
        return 'Assistant';
      case StreamMessageRole.System:
        return 'System';
      case StreamMessageRole.Reasoning:
        return 'Reasoning';
      case StreamMessageRole.Developer:
        return 'Developer';
      case StreamMessageRole.Tool:
        return 'Tool';
      default:
        return 'Unknown';
    }
  }

  private isTextEvent(streamEvent: StreamEventModel): boolean {
    return (
      streamEvent.eventKind === StreamEventKind.TextStart ||
      streamEvent.eventKind === StreamEventKind.TextContent ||
      streamEvent.eventKind === StreamEventKind.TextEnd
    );
  }

  private buildTextRoleLookup(events: StreamEventModel[]): Map<string, StreamMessageRole> {
    const roleByMessageId = new Map<string, StreamMessageRole>();

    for (const streamEvent of events) {
      if (
        streamEvent.eventKind !== StreamEventKind.TextStart ||
        !streamEvent.messageId ||
        streamEvent.messageRole === null ||
        streamEvent.messageRole === undefined
      ) {
        continue;
      }

      roleByMessageId.set(streamEvent.messageId, streamEvent.messageRole);
    }

    return roleByMessageId;
  }
}
