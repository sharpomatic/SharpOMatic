// src/app/services/signalr.service.ts
import { inject, Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { API_URL } from '../components/app/app.tokens';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private readonly apiUrl = inject(API_URL);
  private hubConnection!: signalR.HubConnection;
  private readonly _isConnected = signal(false);
  public readonly isConnected = this._isConnected.asReadonly();

  public startConnection = () => {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.apiUrl}/notifications`, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
      })
      .build();

    this.hubConnection
      .start()
      .then(() => {
        this._isConnected.set(true);
      })
      .catch(err => console.log('Error while starting connection: ' + err));
  }

  public stopConnection = () => {
    if (this.hubConnection) {
      this.hubConnection
        .stop()
        .then(() => {
          console.log('SignalR Connection stopped');
          this._isConnected.set(false);
        })
        .catch(err => console.log('Error while stopping connection: ' + err));
    }
  }

  public addListener(eventName: string, eventHandler: (...args: any[]) => void) {
    if (this.hubConnection) {
      this.hubConnection.on(eventName, eventHandler);
    }
  }

  public removeListener(eventName: string, eventHandler: (...args: any[]) => void) {
    if (this.hubConnection) {
      this.hubConnection.off(eventName, eventHandler);
    }
  }
}
