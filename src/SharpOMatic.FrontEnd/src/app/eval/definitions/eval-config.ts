export interface EvalConfigSnapshot {
  evalConfigId: string;
  workflowId: string | null;
  name: string;
  description: string;
  maxParallel: number;
}

export class EvalConfig {
  private static readonly DEFAULT_MAX_PARALLEL = 1;

  constructor(
    public readonly evalConfigId: string,
    public workflowId: string | null,
    public name: string,
    public description: string,
    public maxParallel: number
  ) {}

  public toSnapshot(): EvalConfigSnapshot {
    return {
      evalConfigId: this.evalConfigId,
      workflowId: this.workflowId ?? null,
      name: this.name,
      description: this.description,
      maxParallel: this.maxParallel,
    };
  }

  public static fromSnapshot(snapshot: EvalConfigSnapshot): EvalConfig {
    return new EvalConfig(
      snapshot.evalConfigId,
      snapshot.workflowId ?? null,
      snapshot.name,
      snapshot.description,
      snapshot.maxParallel
    );
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
