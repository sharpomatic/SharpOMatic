import { effect, inject, Injectable, WritableSignal, signal } from '@angular/core';
import { ConnectionConfig } from '../metadata/definitions/connection-config';
import { ServerRepositoryService } from './server.repository.service';
import { SignalrService } from './signalr.service';

@Injectable({
  providedIn: 'root',
})
export class MetadataService {
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly signalrService = inject(SignalrService);
  private readonly _connectionConfigs: WritableSignal<ConnectionConfig[]> = signal([]);
  readonly connectionConfigs = this._connectionConfigs.asReadonly();

  constructor() {
    effect(() => {
      if (this.signalrService.isConnected()) {
        this.loadConnectionConfigs();
      } else {
        this._connectionConfigs.set([]);
      }
    });
  }

  private loadConnectionConfigs(): void {
    this.serverRepository.getConnectionConfigs().subscribe(configs => {
      this._connectionConfigs.set(configs);
    });
  }
}
