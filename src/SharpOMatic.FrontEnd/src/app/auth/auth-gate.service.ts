import { Injectable, inject, signal } from '@angular/core';
import { AuthTokenService } from './auth-token.service';

export type AuthGateState =
  | 'checking'
  | 'authorized'
  | 'unauthorized'
  | 'forbidden';

@Injectable({
  providedIn: 'root',
})
export class AuthGateService {
  private readonly authTokenService = inject(AuthTokenService);
  private readonly stateSignal = signal<AuthGateState>('checking');

  readonly state = this.stateSignal.asReadonly();

  async initialize(): Promise<void> {
    if (!this.authTokenService.isAuthRequired()) {
      this.stateSignal.set('authorized');
      return;
    }

    try {
      this.stateSignal.set(await this.authTokenService.getAccessState());
    } catch {
      this.stateSignal.set('unauthorized');
    }
  }

  async returnToSignIn(): Promise<void> {
    await this.authTokenService.onUnauthorized();
  }
}
