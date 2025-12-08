import { effect, inject, Injectable, WritableSignal, signal } from '@angular/core';
import { ConnectionConfig } from '../metadata/definitions/connection-config';
import { ModelConfig } from '../metadata/definitions/model-config';
import { ServerRepositoryService } from './server.repository.service';
import { SignalrService } from './signalr.service';

@Injectable({
  providedIn: 'root',
})
export class MetadataService {
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly signalrService = inject(SignalrService);
  private readonly _connectionConfigs: WritableSignal<ConnectionConfig[]> = signal([]);
  private readonly _modelConfigs: WritableSignal<ModelConfig[]> = signal([]);
  readonly connectionConfigs = this._connectionConfigs.asReadonly();
  readonly modelConfigs = this._modelConfigs.asReadonly();

  constructor() {
    effect(() => {
      if (this.signalrService.isConnected()) {
        this.loadConnectionConfigs();
        this.loadModelConfigs();
      } else {
        this._connectionConfigs.set([]);
        this._modelConfigs.set([]);
      }
    });
  }

  private loadConnectionConfigs(): void {
    this.serverRepository.getConnectionConfigs().subscribe(configs => {
      this._connectionConfigs.set(configs);
    });
  }

  private loadModelConfigs(): void {
    this.serverRepository.getModelConfigs().subscribe(configs => {
      this._modelConfigs.set(configs);
    });
  }
}
