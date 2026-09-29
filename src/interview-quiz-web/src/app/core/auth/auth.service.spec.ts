import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MeResponse, TokenResponse } from '../api/contracts';
import { PermissionService } from '../permissions/permission.service';
import { AuthService } from './auth.service';
import { TokenStore } from './token-store.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  let tokens: TokenStore;
  let permissions: PermissionService;

  const tokenResponse: TokenResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
    tokenType: 'Bearer',
  };

  const me: MeResponse = {
    id: 'user-1',
    email: 'admin.dev@example.com',
    permissions: ['openings.read', 'roles.manage'],
  };

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenStore);
    permissions = TestBed.inject(PermissionService);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('login stores tokens and loads permissions', () => {
    auth.login('admin.dev@example.com', 'Dev.Admin!1').subscribe();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.body).toEqual({
      email: 'admin.dev@example.com',
      password: 'Dev.Admin!1',
    });
    login.flush(tokenResponse);
    const meReq = http.expectOne('/api/me');
    meReq.flush(me);
    expect(tokens.accessToken()).toBe('access-1');
    expect(tokens.getRefreshToken()).toBe('refresh-1');
    expect(permissions.hasPermission('openings.read')).toBe(true);
    expect(auth.currentUser()?.email).toBe('admin.dev@example.com');
  });

  it('refreshTokens posts the sessionStorage refresh token and stores the rotation', () => {
    tokens.setSession(tokenResponse);
    auth.refreshTokens().subscribe();
    const refresh = http.expectOne('/api/auth/refresh');
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    refresh.flush({
      ...tokenResponse,
      accessToken: 'access-2',
      refreshToken: 'refresh-2',
    });
    expect(tokens.accessToken()).toBe('access-2');
    expect(tokens.getRefreshToken()).toBe('refresh-2');
  });

  it('logout clears memory and sessionStorage', () => {
    tokens.setSession(tokenResponse);
    permissions.set(['openings.read']);
    auth.logout();
    http.expectOne('/api/auth/logout').flush(null);
    expect(tokens.accessToken()).toBeNull();
    expect(tokens.getRefreshToken()).toBeNull();
    expect(permissions.hasPermission('openings.read')).toBe(false);
  });
});
