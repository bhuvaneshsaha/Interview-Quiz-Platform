import { TestBed } from '@angular/core/testing';
import { CandidateTokenResponse } from '../api/contracts';
import { CandidateSession } from './candidate-session.service';
import { TokenStore } from './token-store.service';

describe('CandidateSession', () => {
  let session: CandidateSession;
  let tokens: TokenStore;

  const candidate: CandidateTokenResponse = {
    accessToken: 'candidate-access',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    tokenType: 'Bearer',
    assignmentId: '7e1a5b54-0f6b-4a3e-bc55-4d2a6f0e5001',
  };

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({});
    session = TestBed.inject(CandidateSession);
    tokens = TestBed.inject(TokenStore);
  });

  afterEach(() => {
    session.leaveAttempt();
    sessionStorage.clear();
  });

  it('keeps the candidate access token in memory only', () => {
    session.enterAttempt();
    session.setSession(candidate);
    expect(session.accessToken()).toBe('candidate-access');
    expect(session.assignmentId()).toBe(candidate.assignmentId);
    expect(session.active()).toBe(true);
    expect(sessionStorage.getItem(TokenStore.refreshStorageKey)).toBeNull();
    expect(tokens.accessToken()).toBeNull();
  });

  it('leaveAttempt clears the candidate token without touching the employee store', () => {
    tokens.setSession({
      accessToken: 'employee-access',
      refreshToken: 'refresh-1',
      accessTokenExpiresAt: '2099-01-01T00:00:00Z',
      refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
      tokenType: 'Bearer',
    });
    session.enterAttempt();
    session.setSession(candidate);
    session.leaveAttempt();
    expect(session.accessToken()).toBeNull();
    expect(session.active()).toBe(false);
    expect(tokens.accessToken()).toBe('employee-access');
    expect(sessionStorage.getItem(TokenStore.refreshStorageKey)).toBe('refresh-1');
  });
});
