import { ModelCapabilities, ModelCapabilitiesSnapshot } from './model-capabilities';
import { FieldDescriptor, FieldDescriptorSnapshot } from './field-descriptor';

export interface ModelConfigSnapshot {
  configId: string;
  displayName: string;
  description: string;
  connectionConfigId: string;
  isCustom: boolean;
  capabilities: ModelCapabilitiesSnapshot;
  parameterFields: FieldDescriptorSnapshot[];
}

export class ModelConfig {
  constructor(
    public readonly configId: string,
    public readonly displayName: string,
    public readonly description: string,
    public readonly connectionConfigId: string,
    public readonly isCustom: boolean,
    public readonly capabilities: ModelCapabilities,
    public readonly parameterFields: FieldDescriptor[],
  ) {}

  public static fromSnapshot(snapshot: ModelConfigSnapshot): ModelConfig {
    return new ModelConfig(
      snapshot.configId,
      snapshot.displayName,
      snapshot.description,
      snapshot.connectionConfigId,
      snapshot.isCustom,
      ModelCapabilities.fromSnapshot(snapshot.capabilities),
      snapshot.parameterFields.map(FieldDescriptor.fromSnapshot),
    );
  }
}
