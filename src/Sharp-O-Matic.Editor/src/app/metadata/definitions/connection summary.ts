export interface ConnectionSummarySnapshot {
  connectionId: string;
  name: string;
  description: string;
}

export class ConnectionSummary {
  constructor(
    public readonly connectionId: string,
    public readonly name: string,
    public readonly description: string
  ) {}

  public static fromSnapshot(snapshot: ConnectionSummarySnapshot): ConnectionSummary {
    return new ConnectionSummary(
      snapshot.connectionId,
      snapshot.name,
      snapshot.description
    );
  }
}
