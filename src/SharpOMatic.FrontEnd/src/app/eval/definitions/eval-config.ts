import { Signal, WritableSignal, computed, signal } from '@angular/core';

export interface EvalConfigSnapshot {
  evalConfigId: string;
  workflowId: string | null;
  name: string;
  description: string;
  maxParallel: number;
}

export class EvalConfig {
  private static readonly DEFAULT_MAX_PARALLEL = 1;

  public readonly evalConfigId: string;
  public workflowId: WritableSignal<string | null>;
  public name: WritableSignal<string>;
  public description: WritableSignal<string>;
  public maxParallel: WritableSignal<number>;
  public readonly isDirty: Signal<boolean>;

  private initialWorkflowId: string | null;
  private initialName: string;
  private initialDescription: string;
  private initialMaxParallel: number;
  private readonly cleanVersion = signal(0);

  constructor(snapshot: EvalConfigSnapshot) {
    this.evalConfigId = snapshot.evalConfigId;
    this.initialWorkflowId = snapshot.workflowId ?? null;
    this.initialName = snapshot.name;
    this.initialDescription = snapshot.description;
    this.initialMaxParallel = snapshot.maxParallel;

    this.workflowId = signal(snapshot.workflowId ?? null);
    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.maxParallel = signal(snapshot.maxParallel);

    this.isDirty = computed(() => {
      this.cleanVersion();

      const currentWorkflowId = this.workflowId();
      const currentName = this.name();
      const currentDescription = this.description();
      const currentMaxParallel = this.maxParallel();

      return currentWorkflowId !== this.initialWorkflowId ||
             currentName !== this.initialName ||
             currentDescription !== this.initialDescription ||
             currentMaxParallel !== this.initialMaxParallel;
    });
  }

  public toSnapshot(): EvalConfigSnapshot {
    return {
      evalConfigId: this.evalConfigId,
      workflowId: this.workflowId() ?? null,
      name: this.name(),
      description: this.description(),
      maxParallel: this.maxParallel(),
    };
  }

  public markClean(): void {
    this.initialWorkflowId = this.workflowId() ?? null;
    this.initialName = this.name();
    this.initialDescription = this.description();
    this.initialMaxParallel = this.maxParallel();
    this.cleanVersion.update(v => v + 1);
  }

  public static fromSnapshot(snapshot: EvalConfigSnapshot): EvalConfig {
    return new EvalConfig(snapshot);
  }

  public static defaultSnapshot(): EvalConfigSnapshot {
    return {
      evalConfigId: crypto.randomUUID(),
      workflowId: null,
      name: '',
      description: '',
      maxParallel: EvalConfig.DEFAULT_MAX_PARALLEL,
    };
  }
}
