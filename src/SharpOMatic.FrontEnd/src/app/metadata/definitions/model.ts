import { Signal, WritableSignal, computed, signal } from '@angular/core';

export interface ModelSnapshot {
  modelId: string;
  version: number;
  name: string;
  description: string;
  connectorId: string | null;
  configId: string;
  customCapabilities: string[];
  parameterValues: Record<string, string | null>;
}

export class Model {
  private static readonly DEFAULT_VERSION = 1;

  public readonly modelId: string;
  public readonly version: number;
  public name: WritableSignal<string>;
  public description: WritableSignal<string>;
  public connectorId: WritableSignal<string | null>;
  public configId: WritableSignal<string>;
  public customCapabilities: WritableSignal<Set<string>>;
  public parameterValues: WritableSignal<Map<string, string | null>>;
  public readonly isDirty: Signal<boolean>;

  private initialName: string;
  private initialDescription: string;
  private initialConnectorId: string | null;
  private initialConfigId: string;
  private initialCustomCapabilities: Set<string>;
  private initialParameterValues: Map<string, string | null>;
  private readonly cleanVersion = signal(0);

  constructor(snapshot: ModelSnapshot) {
    this.modelId = snapshot.modelId;
    this.version = snapshot.version;
    this.initialName = snapshot.name;
    this.initialDescription = snapshot.description;
    this.initialConnectorId = snapshot.connectorId ?? null;
    this.initialConfigId = snapshot.configId;
    this.initialCustomCapabilities = Model.capabilitiesFromSnapshot(
      snapshot.customCapabilities,
    );
    this.initialParameterValues = Model.mapFromSnapshot(
      snapshot.parameterValues,
    );

    this.name = signal(snapshot.name);
    this.description = signal(snapshot.description);
    this.connectorId = signal(snapshot.connectorId ?? null);
    this.configId = signal(snapshot.configId);
    this.customCapabilities = signal(
      Model.capabilitiesFromSnapshot(snapshot.customCapabilities),
    );
    this.parameterValues = signal(
      Model.mapFromSnapshot(snapshot.parameterValues),
    );

    this.isDirty = computed(() => {
      this.cleanVersion();

      const currentName = this.name();
      const currentDescription = this.description();
      const currentConnectorId = this.connectorId();
      const currentConfigId = this.configId();
      const currentCustomCapabilities = this.customCapabilities();
      const currentParameterValues = this.parameterValues();

      const parameterValuesChanged = !Model.areParameterValuesEqual(
        currentParameterValues,
        this.initialParameterValues,
      );

      const capabilitiesChanged = !Model.areCapabilitiesEqual(
        currentCustomCapabilities,
        this.initialCustomCapabilities,
      );

      return (
        currentName !== this.initialName ||
        currentDescription !== this.initialDescription ||
        currentConnectorId !== this.initialConnectorId ||
        currentConfigId !== this.initialConfigId ||
        capabilitiesChanged ||
        parameterValuesChanged
      );
    });
  }

  public toSnapshot(): ModelSnapshot {
    return {
      modelId: this.modelId,
      version: this.version,
      name: this.name(),
      description: this.description(),
      connectorId: this.connectorId() ?? null,
      configId: this.configId(),
      customCapabilities: Model.capabilitiesToSnapshot(
        this.customCapabilities(),
      ),
      parameterValues: Model.snapshotFromMap(this.parameterValues()),
    };
  }

  public static fromSnapshot(snapshot: ModelSnapshot): Model {
    return new Model(snapshot);
  }

  public static defaultSnapshot(): ModelSnapshot {
    return {
      modelId: crypto.randomUUID(),
      version: Model.DEFAULT_VERSION,
      name: '',
      description: '',
      connectorId: null,
      configId: '',
      customCapabilities: [],
      parameterValues: {},
    };
  }

  public markClean(): void {
    this.initialName = this.name();
    this.initialDescription = this.description();
    this.initialConnectorId = this.connectorId() ?? null;
    this.initialConfigId = this.configId();
    this.initialCustomCapabilities = new Set(this.customCapabilities());
    this.initialParameterValues = new Map(this.parameterValues());
    this.cleanVersion.update((v) => v + 1);
  }

  private static mapFromSnapshot(
    parameterValues: ModelSnapshot['parameterValues'] | undefined,
  ): Map<string, string | null> {
    const entries = Object.entries(parameterValues ?? {}).map(
      ([key, value]) => [key, value ?? null] as const,
    );
    return new Map<string, string | null>(entries);
  }

  private static snapshotFromMap(
    parameterValues: Map<string, string | null> | undefined,
  ): ModelSnapshot['parameterValues'] {
    if (!parameterValues) {
      return {};
    }

    const entries = Array.from(parameterValues.entries()).map(
      ([key, value]) => [key, value ?? null] as const,
    );
    return Object.fromEntries(entries) as Record<string, string | null>;
  }

  private static capabilitiesFromSnapshot(
    capabilities: ModelSnapshot['customCapabilities'] | undefined,
  ): Set<string> {
    return new Set(capabilities ?? []);
  }

  private static capabilitiesToSnapshot(
    capabilities: Set<string> | undefined,
  ): ModelSnapshot['customCapabilities'] {
    if (!capabilities) {
      return [];
    }

    return Array.from(capabilities);
  }

  private static areCapabilitiesEqual(
    current: Set<string>,
    initial: Set<string>,
  ): boolean {
    if (current.size !== initial.size) {
      return false;
    }

    for (const capability of current) {
      if (!initial.has(capability)) {
        return false;
      }
    }

    return true;
  }

  private static areParameterValuesEqual(
    current: Map<string, string | null>,
    initial: Map<string, string | null>,
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
}
