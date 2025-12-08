export interface ModelCapabilitiesSnapshot {
  supportsText: boolean;
  supportsTemperature: boolean;
  supportsTopP: boolean;
}

export class ModelCapabilities {
  constructor(
    public readonly supportsText: boolean,
    public readonly supportsTemperature: boolean,
    public readonly supportsTopP: boolean
  ) {}

  public static fromSnapshot(snapshot: ModelCapabilitiesSnapshot): ModelCapabilities {
    return new ModelCapabilities(
      snapshot.supportsText,
      snapshot.supportsTemperature,
      snapshot.supportsTopP,
    );
  }

  public static defaultSnapshot(): ModelCapabilitiesSnapshot {
    return {
      supportsText: false,
      supportsTemperature: false,
      supportsTopP: false,
    };
  }

  public toSnapshot(): ModelCapabilitiesSnapshot {
    return {
      supportsText: this.supportsText,
      supportsTemperature: this.supportsTemperature,
      supportsTopP: this.supportsTopP,
    };
  }

  public equals(other: ModelCapabilities): boolean {
    return this.supportsText === other.supportsText &&
           this.supportsTemperature === other.supportsTemperature &&
           this.supportsTopP === other.supportsTopP;
  }
}
