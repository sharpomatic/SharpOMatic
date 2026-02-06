import { AuthenticationModeKind } from '../enumerations/authentication-mode-kind';
import { FieldDescriptor, FieldDescriptorSnapshot } from './field-descriptor';

export interface AuthenticationModeConfigSnapshot {
  id: string;
  displayName: string;
  kind: AuthenticationModeKind;
  isDefault: boolean;
  fields: FieldDescriptorSnapshot[];
}

export class AuthenticationModeConfig {
  constructor(
    public readonly id: string,
    public readonly displayName: string,
    public readonly kind: AuthenticationModeKind,
    public readonly isDefault: boolean,
    public readonly fields: FieldDescriptor[],
  ) {}

  public static fromSnapshot(
    snapshot: AuthenticationModeConfigSnapshot,
  ): AuthenticationModeConfig {
    return new AuthenticationModeConfig(
      snapshot.id,
      snapshot.displayName,
      snapshot.kind,
      snapshot.isDefault,
      snapshot.fields.map(FieldDescriptor.fromSnapshot),
    );
  }
}
