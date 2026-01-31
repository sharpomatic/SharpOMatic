export interface EvalRowSnapshot {
  evalRowId: string;
  evalConfigId: string;
  order: number;
}

export class EvalRow {
  public readonly evalRowId: string;

  constructor(snapshot: EvalRowSnapshot) {
    this.evalRowId = snapshot.evalRowId;
  }

  public toSnapshot(order: number, evalConfigId: string): EvalRowSnapshot {
    return {
      evalRowId: this.evalRowId,
      evalConfigId,
      order,
    };
  }

  public static fromSnapshot(snapshot: EvalRowSnapshot): EvalRow {
    return new EvalRow(snapshot);
  }

  public static defaultSnapshot(order: number, evalConfigId: string): EvalRowSnapshot {
    return {
      evalRowId: crypto.randomUUID(),
      evalConfigId,
      order,
    };
  }
}
