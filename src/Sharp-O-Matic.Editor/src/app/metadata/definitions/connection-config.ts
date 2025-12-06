import { AuthenticationModeConfig, AuthenticationModeConfigSnapshot } from "./authentication-mode-config";

export interface ConnectionConfigSnapshot {
  id: string;
  displayName: string;
  description: string;
  authModes: AuthenticationModeConfigSnapshot[];
}

export class ConnectionConfig {
  constructor(
    public readonly id: string,
    public readonly displayName: string,
    public readonly description: string,
    public readonly authModes: AuthenticationModeConfig[],
  ) {}

  public static fromSnapshot(snapshot: ConnectionConfigSnapshot): ConnectionConfig {
    return new ConnectionConfig(
      snapshot.id,
      snapshot.displayName,
      snapshot.description,
      snapshot.authModes.map(AuthenticationModeConfig.fromSnapshot),
    );
  }
}
