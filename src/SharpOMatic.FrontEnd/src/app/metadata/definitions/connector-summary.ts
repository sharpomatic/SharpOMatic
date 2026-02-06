export interface ConnectorSummarySnapshot {
  connectorId: string;
  name: string;
  description: string;
}

export class ConnectorSummary {
  constructor(
    public readonly connectorId: string,
    public readonly name: string,
    public readonly description: string,
  ) {}

  public static fromSnapshot(
    snapshot: ConnectorSummarySnapshot,
  ): ConnectorSummary {
    return new ConnectorSummary(
      snapshot.connectorId,
      snapshot.name,
      snapshot.description,
    );
  }
}
