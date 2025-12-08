export interface ModelSummarySnapshot {
  modelId: string;
  name: string;
  description: string;
}

export class ModelSummary {
  constructor(
    public readonly modelId: string,
    public readonly name: string,
    public readonly description: string
  ) {}

  public static fromSnapshot(snapshot: ModelSummarySnapshot): ModelSummary {
    return new ModelSummary(
      snapshot.modelId,
      snapshot.name,
      snapshot.description,
    );
  }
}
