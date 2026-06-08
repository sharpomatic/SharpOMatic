import { computed, Signal, signal, WritableSignal } from '@angular/core';
import { Entity, EntitySnapshot } from './entity.entity';

export interface WorkflowSummarySnapshot extends EntitySnapshot {
  workflowFolderId?: string | null;
  workflowFolderName?: string | null;
  name: string;
  description: string;
  isConversationEnabled: boolean;
}

export class WorkflowSummaryEntity extends Entity<WorkflowSummarySnapshot> {
  public name: WritableSignal<string>;
  public workflowFolderId: WritableSignal<string | null>;
  public workflowFolderName: WritableSignal<string | null>;
  public description: WritableSignal<string>;
  public isConversationEnabled: WritableSignal<boolean>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: WorkflowSummarySnapshot) {
    super(snapshot);

    this.workflowFolderId = signal(snapshot.workflowFolderId ?? null);
    this.workflowFolderName = signal(snapshot.workflowFolderName ?? null);
    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.isConversationEnabled = signal(snapshot.isConversationEnabled);

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentName = this.name();
      const currentWorkflowFolderId = this.workflowFolderId();
      const currentDescription = this.description();
      const currentIsConversationEnabled = this.isConversationEnabled();

      return (
        currentName !== snapshot.name ||
        currentWorkflowFolderId !== (snapshot.workflowFolderId ?? null) ||
        currentDescription !== snapshot.description ||
        currentIsConversationEnabled !== snapshot.isConversationEnabled
      );
    });
  }

  public override toSnapshot(): WorkflowSummarySnapshot {
    return {
      id: this.id,
      version: this.version,
      workflowFolderId: this.workflowFolderId(),
      workflowFolderName: this.workflowFolderName(),
      name: this.name(),
      description: this.description(),
      isConversationEnabled: this.isConversationEnabled(),
    };
  }

  public static override defaultSnapshot() {
    return {
      ...Entity.defaultSnapshot(),
      workflowFolderId: null,
      workflowFolderName: null,
      name: 'Untitled',
      description: '',
      isConversationEnabled: false,
    };
  }

  public static fromSnapshot(
    snapshot: WorkflowSummarySnapshot,
  ): WorkflowSummaryEntity {
    return new WorkflowSummaryEntity(snapshot);
  }
}
