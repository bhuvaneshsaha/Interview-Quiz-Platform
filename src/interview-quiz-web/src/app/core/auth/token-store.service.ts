import { Injectable, signal } from '@angular/core';
import { TokenResponse } from '../api/contracts';

/**
 * Access token lives in memory only. Refresh token lives in sessionStorage
 * (tab-scoped). Neither is written to localStorage, Cache Storage, IndexedDB,
 * or the service worker.
 *
 * Residual risk: XSS in this origin can still read sessionStorage and memory.
 * Logout clears both. See README.
 */
@Injectable({
  providedIn: 'root',
})
export class TokenStore {
  static readonly refreshStorageKey = 'iq.refreshToken';

  private readonly access = signal<string | null>(null);
  readonly accessToken = this.access.asReadonly();

  setSession(tokens: TokenResponse): void {
    this.access.set(tokens.accessToken);
    sessionStorage.setItem(TokenStore.refreshStorageKey, tokens.refreshToken);
  }

  clear(): void {
    this.access.set(null);
    sessionStorage.removeItem(TokenStore.refreshStorageKey);
  }

  getRefreshToken(): string | null {
    return sessionStorage.getItem(TokenStore.refreshStorageKey);
  }

  hasRefreshToken(): boolean {
    const value = this.getRefreshToken();
    return value !== null && value.length > 0;
  }
}
