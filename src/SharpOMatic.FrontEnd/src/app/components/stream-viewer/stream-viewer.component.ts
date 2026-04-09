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
  @Input() streamEvents: StreamEventModel[] = [];

  public readonly StreamEventKind = StreamEventKind;
  public readonly StreamMessageRole = StreamMessageRole;

  public getEventKindLabel(kind: StreamEventKind): string {
    switch (kind) {
      case StreamEventKind.TextStart:
        return 'Text Start';
      case StreamEventKind.TextContent:
        return 'Text Content';
      case StreamEventKind.TextEnd:
        return 'Text End';
      default:
        return 'Unknown';
    }
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
      default:
        return 'Unknown';
    }
  }
}
