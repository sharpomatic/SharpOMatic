import {
  AuthenticationModeConfig,
  AuthenticationModeConfigSnapshot,
} from './authentication-mode-config';

export interface ConnectorConfigSnapshot {
  configId: string;
  version: number;
  displayName: string;
  description: string;
  authModes: AuthenticationModeConfigSnapshot[];
}

export class ConnectorConfig {
  constructor(
    public readonly configId: string,
    public readonly version: number,
    public readonly displayName: string,
    public readonly description: string,
    public readonly authModes: AuthenticationModeConfig[],
  ) {}

  public static fromSnapshot(
    snapshot: ConnectorConfigSnapshot,
  ): ConnectorConfig {
    return new ConnectorConfig(
      snapshot.configId,
      snapshot.version,
      snapshot.displayName,
      snapshot.description,
      snapshot.authModes.map(AuthenticationModeConfig.fromSnapshot),
    );
  }
}
