import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, map, Observable, of, shareReplay, switchMap, tap, throwError } from 'rxjs';
import { LoginRequest, MeResponse, RefreshRequest, TokenResponse } from '../api/contracts';
import { PermissionService } from '../permissions/permission.service';
import { TokenStore } from './token-store.service';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly tokens = inject(TokenStore);
  private readonly permissions = inject(PermissionService);
  private readonly router = inject(Router);

  readonly currentUser = signal<MeResponse | null>(null);
  readonly sessionReady = signal(false);

  private refreshInFlight: Observable<TokenResponse> | null = null;
  private sessionRestore: Observable<boolean> | null = null;
  private loggingOut = false;

  isAuthenticated(): boolean {
    return this.tokens.accessToken() !== null;
  }

  login(email: string, password: string): Observable<void> {
    const body: LoginRequest = { email, password };
    return this.http.post<TokenResponse>('/api/auth/login', body).pipe(
      switchMap((tokens) => this.establishSession(tokens)),
    );
  }

  restoreSession(): Observable<boolean> {
    if (this.sessionReady()) {
      return of(this.isAuthenticated());
    }
    if (!this.sessionRestore) {
      // One restore for the initializer and every guard. A second refresh of the
      // same token would revoke the whole family and bounce a deep link to login.
      this.sessionRestore = this.beginRestore().pipe(
        finalize(() => {
          this.sessionRestore = null;
        }),
        shareReplay(1),
      );
    }
    return this.sessionRestore;
  }

  refreshTokens(): Observable<TokenResponse> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }
    const refreshToken = this.tokens.getRefreshToken();
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token'));
    }
    const body: RefreshRequest = { refreshToken };
    this.refreshInFlight = this.http.post<TokenResponse>('/api/auth/refresh', body).pipe(
      tap((tokens) => this.tokens.setSession(tokens)),
      finalize(() => {
        this.refreshInFlight = null;
      }),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  logout(): void {
    if (this.loggingOut) {
      return;
    }
    this.loggingOut = true;
    const refreshToken = this.tokens.getRefreshToken();
    if (refreshToken) {
      const body: RefreshRequest = { refreshToken };
      this.http.post<void>('/api/auth/logout', body).subscribe({
        error: () => undefined,
      });
    }
    this.clearLocalSession();
    this.loggingOut = false;
    void this.router.navigateByUrl('/login');
  }

  private beginRestore(): Observable<boolean> {
    if (!this.isAuthenticated() && !this.tokens.hasRefreshToken()) {
      this.sessionReady.set(true);
      return of(false);
    }
    const established = this.isAuthenticated()
      ? of(undefined)
      : this.refreshTokens().pipe(map(() => undefined));
    return established.pipe(
      switchMap(() => this.loadProfile()),
      catchError(() => {
        this.clearLocalSession();
        this.sessionReady.set(true);
        return of(false);
      }),
    );
  }

  private establishSession(tokens: TokenResponse): Observable<void> {
    this.tokens.setSession(tokens);
    this.sessionReady.set(false);
    return this.loadProfile().pipe(map(() => undefined));
  }

  private loadProfile(): Observable<boolean> {
    return this.http.get<MeResponse>('/api/me').pipe(
      tap((me) => {
        this.currentUser.set(me);
        this.permissions.set(me.permissions);
        this.sessionReady.set(true);
      }),
      map(() => true),
    );
  }

  private clearLocalSession(): void {
    this.tokens.clear();
    this.permissions.clear();
    this.currentUser.set(null);
    this.refreshInFlight = null;
    this.sessionReady.set(false);
  }
}
