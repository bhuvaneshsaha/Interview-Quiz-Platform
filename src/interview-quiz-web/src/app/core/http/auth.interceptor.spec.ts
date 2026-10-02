import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { TokenStore } from '../auth/token-store.service';
import { TokenResponse } from '../api/contracts';
import { authInterceptor } from './auth.interceptor';
import { CandidateSession } from '../auth/candidate-session.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let tokens: TokenStore;
  let refreshImpl: () => Observable<TokenResponse>;
  let logoutCalls = 0;
  let refreshCalls = 0;

  const rotated: TokenResponse = {
    accessToken: 'access-2',
    refreshToken: 'refresh-2',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
    tokenType: 'Bearer',
  };

  const initial: TokenResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
    tokenType: 'Bearer',
  };

  beforeEach(() => {
    sessionStorage.clear();
    logoutCalls = 0;
    refreshCalls = 0;
    refreshImpl = () => of(rotated);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            refreshTokens: () => {
              refreshCalls += 1;
              return refreshImpl();
            },
            logout: () => {
              logoutCalls += 1;
            },
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
    TestBed.inject(CandidateSession).leaveAttempt();
  });

  it('attaches the in-memory access token as Bearer', () => {
    tokens.setSession(initial);
    http.get('/api/openings').subscribe();
    const req = httpMock.expectOne('/api/openings');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush([]);
  });

  it('does not attach Authorization to login or refresh', () => {
    tokens.setSession(initial);
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
    tokens.setSession(initial);
    refreshImpl = () => {
      tokens.setSession(rotated);
      return of(rotated);
    };

    http.get('/api/openings').subscribe();
    const first = httpMock.expectOne('/api/openings');
    expect(first.request.headers.get('Authorization')).toBe('Bearer access-1');
    first.flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    const retry = httpMock.expectOne('/api/openings');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ items: [] });
    expect(logoutCalls).toBe(0);
  });

  it('logs out when refresh fails after 401', () => {
    tokens.setSession(initial);
    refreshImpl = () => throwError(() => new HttpErrorResponse({ status: 401 }));

    let failed = false;
    http.get('/api/openings').subscribe({
      error: () => {
        failed = true;
      },
    });
    httpMock
      .expectOne('/api/openings')
      .flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    expect(logoutCalls).toBe(1);
    expect(failed).toBe(true);
  });

  it('does not attach Authorization to magic-link consume even when an employee session exists', () => {
    tokens.setSession(initial);
    http.post('/api/auth/magic-link/consume', { token: 'opaque' }).subscribe();
    const consume = httpMock.expectOne('/api/auth/magic-link/consume');
    expect(consume.request.headers.has('Authorization')).toBe(false);
    consume.flush({
      accessToken: 'candidate-1',
      accessTokenExpiresAt: '2099-01-01T00:00:00Z',
      tokenType: 'Bearer',
      assignmentId: '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001',
    });
  });

  it('sends the candidate bearer on attempt APIs from the attempt app, not the employee token', () => {
    tokens.setSession(initial);
    const candidate = TestBed.inject(CandidateSession);
    candidate.enterAttempt();
    candidate.setSession({
      accessToken: 'candidate-1',
      accessTokenExpiresAt: '2099-01-01T00:00:00Z',
      tokenType: 'Bearer',
      assignmentId: '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001',
    });

    http.post('/api/assignments/7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001/attempts', {}).subscribe();
    const start = httpMock.expectOne(
      '/api/assignments/7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001/attempts',
    );
    expect(start.request.headers.get('Authorization')).toBe('Bearer candidate-1');
    start.flush({});

    http.get('/api/openings').subscribe();
    const openings = httpMock.expectOne('/api/openings');
    expect(openings.request.headers.get('Authorization')).toBe('Bearer access-1');
    openings.flush([]);
    candidate.leaveAttempt();
  });

  it('does not run employee refresh on a candidate 401', () => {
    tokens.setSession(initial);
    const candidate = TestBed.inject(CandidateSession);
    candidate.enterAttempt();
    candidate.setSession({
      accessToken: 'candidate-1',
      accessTokenExpiresAt: '2099-01-01T00:00:00Z',
      tokenType: 'Bearer',
      assignmentId: '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001',
    });

    let failed = false;
    http.get('/api/assignments/7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001/attempts/current').subscribe({
      error: () => {
        failed = true;
      },
    });
    httpMock
      .expectOne('/api/assignments/7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001/attempts/current')
      .flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    expect(refreshCalls).toBe(0);
    expect(logoutCalls).toBe(0);
    expect(failed).toBe(true);
    expect(tokens.accessToken()).toBe('access-1');
    candidate.leaveAttempt();
  });
});
