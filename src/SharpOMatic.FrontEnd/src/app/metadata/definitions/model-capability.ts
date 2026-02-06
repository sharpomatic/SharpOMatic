export interface ModelCapabilitySnapshot {
  name: string;
  displayName: string;
}

export class ModelCapability {
  constructor(
    public readonly name: string,
    public readonly displayName: string,
  ) {}

  public static fromSnapshot(
    snapshot: ModelCapabilitySnapshot,
  ): ModelCapability {
    return new ModelCapability(snapshot.name, snapshot.displayName);
  }
}
