import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { TokenStore } from '../auth/token-store.service';
import { TokenResponse } from '../api/contracts';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let tokens: TokenStore;
  let refreshSpy: ReturnType<typeof vi.fn>;
  let logoutSpy: ReturnType<typeof vi.fn>;

  const rotated: TokenResponse = {
    accessToken: 'access-2',
    refreshToken: 'refresh-2',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
    tokenType: 'Bearer',
  };

  beforeEach(() => {
    sessionStorage.clear();
    refreshSpy = vi.fn();
    logoutSpy = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            refreshTokens: () => refreshSpy(),
            logout: () => logoutSpy(),
          },
        },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenStore);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('attaches the in-memory access token as Bearer', () => {
    tokens.setSession({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: '2099-01-01T00:00:00Z',
      refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
      tokenType: 'Bearer',
    });
    http.get('/api/openings').subscribe();
    const req = httpMock.expectOne('/api/openings');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush([]);
  });

  it('does not attach Authorization to login or refresh', () => {
    tokens.setSession({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: '2099-01-01T00:00:00Z',
      refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
      tokenType: 'Bearer',
    });
    http.post('/api/auth/login', { email: 'a@b.c', password: 'x' }).subscribe();
    const login = httpMock.expectOne('/api/auth/login');
    expect(login.request.headers.has('Authorization')).toBe(false);
    login.flush({});
    http.post('/api/auth/refresh', { refreshToken: 'refresh-1' }).subscribe();
    const refresh = httpMock.expectOne('/api/auth/refresh');
    expect(refresh.request.headers.has('Authorization')).toBe(false);
    refresh.flush({});
  });

  it('refreshes once on 401 then retries with the new access token', () => {
    tokens.setSession({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: '2099-01-01T00:00:00Z',
      refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
      tokenType: 'Bearer',
    });
    refreshSpy.mockImplementation(() => {
      tokens.setSession(rotated);
      return of(rotated);
    });

    http.get('/api/openings').subscribe();
    const first = httpMock.expectOne('/api/openings');
    expect(first.request.headers.get('Authorization')).toBe('Bearer access-1');
    first.flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    const retry = httpMock.expectOne('/api/openings');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ items: [] });
    expect(logoutSpy).not.toHaveBeenCalled();
  });

  it('logs out when refresh fails after 401', () => {
    tokens.setSession({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: '2099-01-01T00:00:00Z',
      refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
      tokenType: 'Bearer',
    });
    refreshSpy.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));

    let failed = false;
    http.get('/api/openings').subscribe({
      error: () => {
        failed = true;
      },
    });
    httpMock.expectOne('/api/openings').flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    expect(logoutSpy).toHaveBeenCalled();
    expect(failed).toBe(true);
  });
});
