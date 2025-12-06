import { signal, WritableSignal } from '@angular/core';

export interface ConnectionSnapshot {
  connectionId: string;
  name: string;
  description: string;
  configId: string;
  authenticationModeId: string;
  fieldValues: Record<string, string | null>;
}

export class Connection {
  public readonly connectionId: string;
  public name: WritableSignal<string>;
  public description: WritableSignal<string>;
  public configId: WritableSignal<string>;
  public authenticationModeId: WritableSignal<string>;
  public fieldValues: WritableSignal<Map<string, string | null>>;

  constructor(snapshot: ConnectionSnapshot) {
    this.connectionId = snapshot.connectionId;
    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.configId = signal(snapshot.configId);
    this.authenticationModeId = signal(snapshot.authenticationModeId);
    this.fieldValues = signal(Connection.mapFromSnapshot(snapshot.fieldValues));
  }

  public toSnapshot(): ConnectionSnapshot {
    return {
      connectionId: this.connectionId,
      name: this.name(),
      description: this.description(),
      configId: this.configId(),
      authenticationModeId: this.authenticationModeId(),
      fieldValues: Connection.snapshotFromMap(this.fieldValues()),
    };
  }

  public static fromSnapshot(snapshot: ConnectionSnapshot): Connection {
    return new Connection(snapshot);
  }

  public static defaultSnapshot(): ConnectionSnapshot {
    return {
      connectionId: crypto.randomUUID(),
      name: '',
      description: '',
      configId: '',
      authenticationModeId: '',
      fieldValues: {},
    };
  }

  private static mapFromSnapshot(fieldValues: ConnectionSnapshot['fieldValues'] | undefined): Map<string, string | null> {
    const entries = Object.entries(fieldValues ?? {}).map(([key, value]) => [key, value ?? null] as const);
    return new Map<string, string | null>(entries);
  }

  private static snapshotFromMap(fieldValues: Map<string, string | null> | undefined): ConnectionSnapshot['fieldValues'] {
    if (!fieldValues) {
      return {};
    }

    const entries = Array.from(fieldValues.entries()).map(([key, value]) => [key, value ?? null] as const);
    return Object.fromEntries(entries) as Record<string, string | null>;
  }
}
