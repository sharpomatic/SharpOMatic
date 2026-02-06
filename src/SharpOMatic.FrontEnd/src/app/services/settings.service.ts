import {
  Injectable,
  Signal,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import { API_URL } from '../components/app/app.tokens';

@Injectable({
  providedIn: 'root',
})
export class SettingsService {
  private readonly storageKey = 'apiUrl';
  private readonly defaultApiUrl = inject(API_URL);
  private readonly apiUrlSignal: WritableSignal<string>;
  readonly apiUrlState: Signal<string>;

  constructor() {
    const stored = this.readStoredApiUrl();
    this.apiUrlSignal = signal(stored ?? this.defaultApiUrl);
    this.apiUrlState = this.apiUrlSignal.asReadonly();
  }

  apiUrl(): string {
    return this.apiUrlSignal();
  }

  setApiUrl(url: string): void {
    const trimmed = url?.trim();
    const next = trimmed?.length ? trimmed : this.defaultApiUrl;
    this.apiUrlSignal.set(next);
    this.writeStoredApiUrl(next);
  }

  private readStoredApiUrl(): string | null {
    try {
      const stored = localStorage.getItem(this.storageKey);
      if (stored && stored.trim().length) {
        return stored.trim();
      }
    } catch {}
    return null;
  }

  private writeStoredApiUrl(value: string): void {
    try {
      localStorage.setItem(this.storageKey, value);
    } catch {}
  }
}
