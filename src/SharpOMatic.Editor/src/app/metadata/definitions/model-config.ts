import { ModelCapability, ModelCapabilitySnapshot } from './model-capability';
import { FieldDescriptor, FieldDescriptorSnapshot } from './field-descriptor';

export interface ModelConfigSnapshot {
  configId: string;
  displayName: string;
  description: string;
  connectorConfigId: string;
  isCustom: boolean;
  capabilities: ModelCapabilitySnapshot[];
  parameterFields: FieldDescriptorSnapshot[];
}

export class ModelConfig {
  constructor(
    public readonly configId: string,
    public readonly displayName: string,
    public readonly description: string,
    public readonly connectorConfigId: string,
    public readonly isCustom: boolean,
    public readonly capabilities: ModelCapability[],
    public readonly parameterFields: FieldDescriptor[],
  ) {}

  public static fromSnapshot(snapshot: ModelConfigSnapshot): ModelConfig {
    return new ModelConfig(
      snapshot.configId,
      snapshot.displayName,
      snapshot.description,
      snapshot.connectorConfigId,
      snapshot.isCustom,
      snapshot.capabilities.map(ModelCapability.fromSnapshot),
      snapshot.parameterFields.map(FieldDescriptor.fromSnapshot),
    );
  }
}
