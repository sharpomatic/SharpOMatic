export interface ConnectorSummarySnapshot {
  connectorId: string;
  created?: string | null;
  modified?: string | null;
  name: string;
  description: string;
}

export class ConnectorSummary {
  constructor(
    public readonly connectorId: string,
    public readonly name: string,
    public readonly description: string,
    public readonly created: string | null,
    public readonly modified: string | null,
  ) {}

  public static fromSnapshot(
    snapshot: ConnectorSummarySnapshot,
  ): ConnectorSummary {
    return new ConnectorSummary(
      snapshot.connectorId,
      snapshot.name,
      snapshot.description,
      snapshot.created ?? null,
      snapshot.modified ?? null,
    );
  }
}
