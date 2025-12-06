import { signal, WritableSignal } from '@angular/core';

export interface ConnectionSnapshot {
  connectionId: string;
  name: string;
  description: string;
  configId: string;
  authenticationModeId: string;
  fieldValues: object;
}

export class Connection {
  public readonly connectionId: string;
  public name: WritableSignal<string>;
  public description: WritableSignal<string>;
  public configId: WritableSignal<string>;
  public authenticationModeId: WritableSignal<string>;
  public fieldValues: WritableSignal<object>;

  constructor(snapshot: ConnectionSnapshot) {
    this.connectionId = snapshot.connectionId;
    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.configId = signal(snapshot.configId);
    this.authenticationModeId = signal(snapshot.authenticationModeId);
    this.fieldValues = signal(snapshot.fieldValues);
  }

  public toSnapshot(): ConnectionSnapshot {
    return {
      connectionId: this.connectionId,
      name: this.name(),
      description: this.description(),
      configId: this.configId(),
      authenticationModeId: this.authenticationModeId(),
      fieldValues: this.fieldValues(),
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
}
