import { effect, inject, Injectable, WritableSignal, signal } from '@angular/core';
import { ConnectorConfig } from '../metadata/definitions/connector-config';
import { ModelConfig } from '../metadata/definitions/model-config';
import { ServerRepositoryService } from './server.repository.service';
import { SignalrService } from './signalr.service';

@Injectable({
  providedIn: 'root',
})
export class MetadataService {
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly signalrService = inject(SignalrService);
  private readonly _connectorConfigs: WritableSignal<ConnectorConfig[]> = signal([]);
  private readonly _modelConfigs: WritableSignal<ModelConfig[]> = signal([]);
  readonly connectorConfigs = this._connectorConfigs.asReadonly();
  readonly modelConfigs = this._modelConfigs.asReadonly();

  constructor() {
    effect(() => {
      if (this.signalrService.isConnected()) {
        this.loadConnectorConfigs();
        this.loadModelConfigs();
      } else {
        this._connectorConfigs.set([]);
        this._modelConfigs.set([]);
      }
    });
  }

  private loadConnectorConfigs(): void {
    this.serverRepository.getConnectorConfigs().subscribe(configs => {
      const sorted = [...configs].sort((a, b) => a.displayName.localeCompare(b.displayName));
      this._connectorConfigs.set(sorted);
    });
  }

  private loadModelConfigs(): void {
    this.serverRepository.getModelConfigs().subscribe(configs => {
      this._modelConfigs.set(configs);
    });
  }
}
