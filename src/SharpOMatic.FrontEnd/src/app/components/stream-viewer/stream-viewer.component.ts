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
        return 'TEXT_MESSAGE_START';
      case StreamEventKind.TextContent:
        return 'TEXT_MESSAGE_CONTENT';
      case StreamEventKind.TextEnd:
        return 'TEXT_MESSAGE_END';
      case StreamEventKind.ReasoningStart:
        return 'REASONING_START';
      case StreamEventKind.ReasoningMessageStart:
        return 'REASONING_MESSAGE_START';
      case StreamEventKind.ReasoningMessageContent:
        return 'REASONING_MESSAGE_CONTENT';
      case StreamEventKind.ReasoningMessageEnd:
        return 'REASONING_MESSAGE_END';
      case StreamEventKind.ReasoningEnd:
        return 'REASONING_END';
      case StreamEventKind.ToolCallStart:
        return 'TOOL_CALL_START';
      case StreamEventKind.ToolCallArgs:
        return 'TOOL_CALL_ARGS';
      case StreamEventKind.ToolCallEnd:
        return 'TOOL_CALL_END';
      case StreamEventKind.ToolCallResult:
        return 'TOOL_CALL_RESULT';
      case StreamEventKind.ActivitySnapshot:
        return 'ACTIVITY_SNAPSHOT';
      case StreamEventKind.ActivityDelta:
        return 'ACTIVITY_DELTA';
      case StreamEventKind.StepStart:
        return 'STEP_STARTED';
      case StreamEventKind.StepEnd:
        return 'STEP_FINISHED';
      case StreamEventKind.StateSnapshot:
        return 'STATE_SNAPSHOT';
      case StreamEventKind.StateDelta:
        return 'STATE_DELTA';
      case StreamEventKind.Custom:
        return 'CUSTOM';
      default:
        return 'UNKNOWN';
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
