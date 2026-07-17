export interface EvalConfigSummarySnapshot {
  evalConfigId: string;
  created?: string | null;
  modified?: string | null;
  name: string;
  description: string;
}

export class EvalConfigSummary {
  constructor(
    public readonly evalConfigId: string,
    public readonly name: string,
    public readonly description: string,
    public readonly created: string | null,
    public readonly modified: string | null,
  ) {}

  public static fromSnapshot(
    snapshot: EvalConfigSummarySnapshot,
  ): EvalConfigSummary {
    return new EvalConfigSummary(
      snapshot.evalConfigId,
      snapshot.name,
      snapshot.description,
      snapshot.created ?? null,
      snapshot.modified ?? null,
    );
  }
}
