import { FieldDescriptorType } from '../enumerations/field-descriptor-type';

export interface FieldDescriptorSnapshot {
  name: string;
  label: string;
  description: string;
  callDefined: boolean;
  type: FieldDescriptorType;
  isRequired: boolean;
  capability: string | null;
  defaultValue: any;
  enumOptions: string[] | null;
  min: number | null;
  max: number | null;
  step: number | null;
}

export class FieldDescriptor {
  constructor(
    public readonly name: string,
    public readonly label: string,
    public readonly description: string,
    public readonly callDefined: boolean,
    public readonly type: FieldDescriptorType,
    public readonly isRequired: boolean,
    public readonly capability: string | null,
    public readonly defaultValue: any,
    public readonly enumOptions: string[] | null,
    public readonly min: number | null,
    public readonly max: number | null,
    public readonly step: number | null
  ) {}

  public static fromSnapshot(snapshot: FieldDescriptorSnapshot): FieldDescriptor {
    return new FieldDescriptor(
      snapshot.name,
      snapshot.label,
      snapshot.description,
      snapshot.callDefined,
      snapshot.type,
      snapshot.isRequired,
      snapshot.capability,
      snapshot.defaultValue,
      snapshot.enumOptions ? [...snapshot.enumOptions] : null,
      snapshot.min,
      snapshot.max,
      snapshot.step,
    );
  }
}
