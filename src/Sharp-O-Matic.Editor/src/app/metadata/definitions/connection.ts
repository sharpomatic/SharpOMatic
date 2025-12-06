import { Signal, computed, signal, WritableSignal } from '@angular/core';

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
  public readonly isDirty: Signal<boolean>;

  private initialName: string;
  private initialDescription: string;
  private initialConfigId: string;
  private initialAuthenticationModeId: string;
  private initialFieldValues: Map<string, string | null>;
  private readonly cleanVersion = signal(0);

  constructor(snapshot: ConnectionSnapshot) {
    this.connectionId = snapshot.connectionId;
    this.initialName = snapshot.name;
    this.initialDescription = snapshot.description;
    this.initialConfigId = snapshot.configId;
    this.initialAuthenticationModeId = snapshot.authenticationModeId;
    this.initialFieldValues = Connection.mapFromSnapshot(snapshot.fieldValues);

    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.configId = signal(snapshot.configId);
    this.authenticationModeId = signal(snapshot.authenticationModeId);
    this.fieldValues = signal(Connection.mapFromSnapshot(snapshot.fieldValues));

    this.isDirty = computed(() => {
      this.cleanVersion();

      const currentName = this.name();
      const currentDescription = this.description();
      const currentConfigId = this.configId();
      const currentAuthenticationModeId = this.authenticationModeId();
      const currentFieldValues = this.fieldValues();

      const fieldValuesChanged = !Connection.areFieldValuesEqual(currentFieldValues, this.initialFieldValues);

      return currentName !== this.initialName ||
             currentDescription !== this.initialDescription ||
             currentConfigId !== this.initialConfigId ||
             currentAuthenticationModeId !== this.initialAuthenticationModeId ||
             fieldValuesChanged;
    });
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

  public markClean(): void {
    this.initialName = this.name();
    this.initialDescription = this.description();
    this.initialConfigId = this.configId();
    this.initialAuthenticationModeId = this.authenticationModeId();
    this.initialFieldValues = new Map(this.fieldValues());
    this.cleanVersion.update(v => v + 1);
  }

  private static mapFromSnapshot(fieldValues: ConnectionSnapshot['fieldValues'] | undefined): Map<string, string | null> {
    const entries = Object.entries(fieldValues ?? {}).map(([key, value]) => [key, value ?? null] as const);
    return new Map<string, string | null>(entries);
  }

  private static areFieldValuesEqual(
    current: Map<string, string | null>,
    initial: Map<string, string | null>
  ): boolean {
    if (current.size !== initial.size) {
      return false;
    }

    for (const [key, value] of current.entries()) {
      if (!initial.has(key) || initial.get(key) !== value) {
        return false;
      }
    }

    return true;
  }

  private static snapshotFromMap(fieldValues: Map<string, string | null> | undefined): ConnectionSnapshot['fieldValues'] {
    if (!fieldValues) {
      return {};
    }

    const entries = Array.from(fieldValues.entries()).map(([key, value]) => [key, value ?? null] as const);
    return Object.fromEntries(entries) as Record<string, string | null>;
  }
}
