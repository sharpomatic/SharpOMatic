import { FieldDescriptorType } from '../enumerations/field-descriptor-type';

export interface FieldDescriptorSnapshot {
  name: string;
  label: string;
  description: string;
  type: FieldDescriptorType;
  isRequired: boolean;
  defaultValue: unknown;
  enumOptions: string[];
}

export class FieldDescriptor {
  constructor(
    public readonly name: string,
    public readonly label: string,
    public readonly description: string,
    public readonly type: FieldDescriptorType,
    public readonly isRequired: boolean,
    public readonly defaultValue: unknown,
    public readonly enumOptions: string[],
  ) {}

  public static fromSnapshot(snapshot: FieldDescriptorSnapshot): FieldDescriptor {
    return new FieldDescriptor(
      snapshot.name,
      snapshot.label,
      snapshot.description,
      snapshot.type,
      snapshot.isRequired,
      snapshot.defaultValue,
      [...snapshot.enumOptions],
    );
  }
}