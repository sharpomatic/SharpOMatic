import { FieldDescriptorType } from '../enumerations/field-descriptor-type';

export interface ModelInformationSnapshot {
  name: string;
  displayName?: string | null;
  type: FieldDescriptorType;
  value: unknown;
}

export class ModelInformation {
  constructor(
    public readonly name: string,
    public readonly displayName: string,
    public readonly type: FieldDescriptorType,
    public readonly value: unknown,
  ) {}

  public static fromSnapshot(
    snapshot: ModelInformationSnapshot,
  ): ModelInformation {
    return new ModelInformation(
      snapshot.name,
      snapshot.displayName ?? snapshot.name,
      snapshot.type,
      snapshot.value,
    );
  }
}
