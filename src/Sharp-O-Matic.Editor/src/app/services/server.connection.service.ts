import { HttpClient } from '@angular/common/http';
import { Injectable, OnDestroy, effect, inject, signal } from '@angular/core';
import { catchError } from 'rxjs';
import { API_URL } from '../components/app/app.tokens';
import { SignalrService } from './signalr.service';
import { of } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class ServerConnectionService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_URL);
  private readonly signalrService = inject(SignalrService);
  private readonly _isConnected = signal(false);
  public readonly isConnected = this._isConnected.asReadonly();
  private statusCheckInterval?: number;

  constructor() {
    effect(() => {
      if (this.isConnected()) {
        this.signalrService.startConnection();
      } else {
        this.signalrService.stopConnection();
      }
    });

    this.startStatusCheck();
  }

  public startStatusCheck(): void {
    if (this.statusCheckInterval) {
      return;
    }

    this.checkStatus();
    this.statusCheckInterval = window.setInterval(() => this.checkStatus(), 5000);
  }

  private checkStatus(): void {
    this.http.get(`${this.apiUrl}/api/status`, { observe: 'response', responseType: 'text' })
      .pipe(catchError(() => of(null)))
      .subscribe(response => {
        this._isConnected.set(response?.ok ?? false);
      });
  }

  ngOnDestroy(): void {
    if (this.statusCheckInterval) {
      clearInterval(this.statusCheckInterval);
    }
  }
}
