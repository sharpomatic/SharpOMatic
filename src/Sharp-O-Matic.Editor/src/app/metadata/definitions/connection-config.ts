import { AuthenticationModeConfig, AuthenticationModeConfigSnapshot } from "./authentication-mode-config";

export interface ConnectionConfigSnapshot {
  configId: string;
  displayName: string;
  description: string;
  authModes: AuthenticationModeConfigSnapshot[];
}

export class ConnectionConfig {
  constructor(
    public readonly configId: string,
    public readonly displayName: string,
    public readonly description: string,
    public readonly authModes: AuthenticationModeConfig[],
  ) {}

  public static fromSnapshot(snapshot: ConnectionConfigSnapshot): ConnectionConfig {
    return new ConnectionConfig(
      snapshot.configId,
      snapshot.displayName,
      snapshot.description,
      snapshot.authModes.map(AuthenticationModeConfig.fromSnapshot),
    );
  }
}
