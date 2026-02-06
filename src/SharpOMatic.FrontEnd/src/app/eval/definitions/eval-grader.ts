import { WritableSignal, signal } from '@angular/core';

export interface EvalGraderSnapshot {
  evalGraderId: string;
  evalConfigId: string;
  workflowId: string | null;
  label: string;
  passThreshold: number;
  order: number;
}

export class EvalGrader {
  public readonly evalGraderId: string;
  public workflowId: WritableSignal<string | null>;
  public label: WritableSignal<string>;
  public passThreshold: WritableSignal<number>;

  constructor(snapshot: EvalGraderSnapshot) {
    this.evalGraderId = snapshot.evalGraderId;
    this.workflowId = signal(snapshot.workflowId ?? null);
    this.label = signal(snapshot.label);
    this.passThreshold = signal(snapshot.passThreshold);
  }

  public toSnapshot(order: number, evalConfigId: string): EvalGraderSnapshot {
    return {
      evalGraderId: this.evalGraderId,
      evalConfigId,
      workflowId: this.workflowId() ?? null,
      label: this.label(),
      passThreshold: this.passThreshold(),
      order,
    };
  }

  public static fromSnapshot(snapshot: EvalGraderSnapshot): EvalGrader {
    return new EvalGrader(snapshot);
  }

  public static defaultSnapshot(
    order: number,
    evalConfigId: string,
  ): EvalGraderSnapshot {
    return {
      evalGraderId: crypto.randomUUID(),
      evalConfigId,
      workflowId: null,
      label: '',
      passThreshold: 0,
      order,
    };
  }
}
