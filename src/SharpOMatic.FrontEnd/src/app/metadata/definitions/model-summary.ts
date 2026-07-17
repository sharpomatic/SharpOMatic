export interface ModelSummarySnapshot {
  modelId: string;
  created?: string | null;
  modified?: string | null;
  name: string;
  description: string;
}

export class ModelSummary {
  constructor(
    public readonly modelId: string,
    public readonly name: string,
    public readonly description: string,
    public readonly created: string | null,
    public readonly modified: string | null,
  ) {}

  public static fromSnapshot(snapshot: ModelSummarySnapshot): ModelSummary {
    return new ModelSummary(
      snapshot.modelId,
      snapshot.name,
      snapshot.description,
      snapshot.created ?? null,
      snapshot.modified ?? null,
    );
  }
}
