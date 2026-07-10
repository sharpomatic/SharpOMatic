import { WritableSignal, signal } from '@angular/core';

export interface EvalRowSnapshot {
  evalRowId: string;
  evalConfigId: string;
  order: number;
  repeat?: number | null;
}

export class EvalRow {
  public static readonly DEFAULT_REPEAT = 1;
  public static readonly MIN_REPEAT = 0;
  public static readonly MAX_REPEAT = 10000;

  public readonly evalRowId: string;
  public repeat: WritableSignal<number>;

  constructor(snapshot: EvalRowSnapshot) {
    this.evalRowId = snapshot.evalRowId;
    this.repeat = signal(EvalRow.normalizeRepeat(snapshot.repeat));
  }

  public toSnapshot(order: number, evalConfigId: string): EvalRowSnapshot {
    return {
      evalRowId: this.evalRowId,
      evalConfigId,
      order,
      repeat: this.repeat(),
    };
  }

  public static fromSnapshot(snapshot: EvalRowSnapshot): EvalRow {
    return new EvalRow(snapshot);
  }

  public static defaultSnapshot(
    order: number,
    evalConfigId: string,
  ): EvalRowSnapshot {
    return {
      evalRowId: crypto.randomUUID(),
      evalConfigId,
      order,
      repeat: EvalRow.DEFAULT_REPEAT,
    };
  }

  public static normalizeRepeat(value: string | number | null | undefined): number {
    const numeric = Number(value ?? EvalRow.DEFAULT_REPEAT);
    if (!Number.isFinite(numeric)) {
      return EvalRow.DEFAULT_REPEAT;
    }

    return Math.max(
      EvalRow.MIN_REPEAT,
      Math.min(EvalRow.MAX_REPEAT, Math.trunc(numeric)),
    );
  }
}
