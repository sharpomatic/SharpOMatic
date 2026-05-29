import './auth-provider';

import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AuthTokenService {
  isAuthRequired(): boolean {
    return window.sharpomaticAuth?.required === true;
  }

  async getAuthorizationHeader(): Promise<string | null> {
    const token = await this.getRawToken();
    return token ? `Bearer ${token}` : null;
  }

  async getRawToken(): Promise<string> {
    const token = await window.sharpomaticAuth?.getBearerToken?.();
    return this.normalizeToken(token);
  }

  async onUnauthorized(): Promise<void> {
    await window.sharpomaticAuth?.onUnauthorized?.();
  }

  private normalizeToken(token: string | null | undefined): string {
    return token?.replace(/^Bearer\s+/i, '').trim() ?? '';
  }
}
