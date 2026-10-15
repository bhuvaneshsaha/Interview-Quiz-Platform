import { Injectable, signal } from '@angular/core';
import { CandidateTokenResponse } from '../api/contracts';

/**
 * Assignment-scoped candidate access token (ADR 0008). In-memory only.
 * Never writes `iq.refreshToken`, TokenStore, sessionStorage, localStorage,
 * IndexedDB, or the service worker. No refresh token.
 */
@Injectable({
  providedIn: 'root',
})
export class CandidateSession {
  private readonly access = signal<string | null>(null);
  private readonly assignment = signal<string | null>(null);
  private readonly attemptApp = signal(false);
  private readonly invite = signal<string | null>(null);

  readonly accessToken = this.access.asReadonly();
  readonly assignmentId = this.assignment.asReadonly();
  readonly active = this.attemptApp.asReadonly();
  readonly inviteToken = this.invite.asReadonly();

  enterAttempt(): void {
    this.attemptApp.set(true);
  }

  leaveAttempt(): void {
    this.attemptApp.set(false);
    this.clear();
    this.invite.set(null);
  }

  setInviteToken(token: string): void {
    const trimmed = token.trim();
    this.invite.set(trimmed.length > 0 ? trimmed : null);
  }

  setSession(tokens: CandidateTokenResponse): void {
    this.access.set(tokens.accessToken);
    this.assignment.set(tokens.assignmentId);
  }

  clear(): void {
    this.access.set(null);
    this.assignment.set(null);
  }

  hasAccessToken(): boolean {
    const value = this.access();
    return value !== null && value.length > 0;
  }
}
