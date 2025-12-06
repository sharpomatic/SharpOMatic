export interface ConnectionSnapshot {
  connectionId: string;
  name: string;
  description: string;
  configId: string;
  authenticationModeId: string;
  fieldValues: object;
}

export class Connection {
  constructor(
    public readonly connectionId: string,
    public readonly name: string,
    public readonly description: string,
    public readonly configId: string,
    public readonly authenticationModeId: string,
    public readonly fieldValues: object
  ) {}

  public static fromSnapshot(snapshot: ConnectionSnapshot): Connection {
    return new Connection(
      snapshot.connectionId,
      snapshot.name,
      snapshot.description,
      snapshot.configId,
      snapshot.authenticationModeId,
      snapshot.fieldValues,
    );
  }
}
