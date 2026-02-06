// src/app/services/signalr.service.ts
import { effect, inject, Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { SettingsService } from './settings.service';

@Injectable({
  providedIn: 'root',
})
export class SignalrService {
  private readonly settingsService = inject(SettingsService);
  private hubConnection?: signalR.HubConnection;
  private readonly _isConnected = signal(false);
  public readonly isConnected = this._isConnected.asReadonly();
  private currentApiUrl: string = '';

  constructor() {
    effect(() => {
      const nextApiUrl = this.settingsService.apiUrlState();
      if (nextApiUrl === this.currentApiUrl) {
        return;
      }
      this.currentApiUrl = nextApiUrl;
      this.restartConnection();
    });
  }

  public startConnection = () => {
    const apiUrl = this.currentApiUrl ?? this.settingsService.apiUrl();
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiUrl}/notifications`, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .build();

    this.hubConnection
      .start()
      .then(() => {
        this._isConnected.set(true);
      })
      .catch((err) => console.log('Error while starting connection: ' + err));
  };

  public stopConnection = (): Promise<void> => {
    if (!this.hubConnection) {
      return Promise.resolve();
    }

    return this.hubConnection
      .stop()
      .then(() => {
        console.log('SignalR Connection stopped');
        this._isConnected.set(false);
      })
      .catch((err) => {
        console.log('Error while stopping connection: ' + err);
      })
      .finally(() => {
        this.hubConnection = undefined;
      });
  };

  private restartConnection(): void {
    this.stopConnection().finally(() => this.startConnection());
  }

  public addListener(
    eventName: string,
    eventHandler: (...args: any[]) => void,
  ) {
    if (this.hubConnection) {
      this.hubConnection.on(eventName, eventHandler);
    }
  }

  public removeListener(
    eventName: string,
    eventHandler: (...args: any[]) => void,
  ) {
    if (this.hubConnection) {
      this.hubConnection.off(eventName, eventHandler);
    }
  }
}
