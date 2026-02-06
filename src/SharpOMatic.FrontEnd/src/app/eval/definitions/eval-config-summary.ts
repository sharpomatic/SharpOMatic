export interface EvalConfigSummarySnapshot {
  evalConfigId: string;
  name: string;
  description: string;
}

export class EvalConfigSummary {
  constructor(
    public readonly evalConfigId: string,
    public readonly name: string,
    public readonly description: string,
  ) {}

  public static fromSnapshot(
    snapshot: EvalConfigSummarySnapshot,
  ): EvalConfigSummary {
    return new EvalConfigSummary(
      snapshot.evalConfigId,
      snapshot.name,
      snapshot.description,
    );
  }
}
