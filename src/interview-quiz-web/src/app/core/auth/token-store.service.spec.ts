import { TestBed } from '@angular/core/testing';
import { TokenResponse } from '../api/contracts';
import { TokenStore } from './token-store.service';

describe('TokenStore', () => {
  let store: TokenStore;

  const tokens: TokenResponse = {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
    tokenType: 'Bearer',
  };

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({});
    store = TestBed.inject(TokenStore);
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('keeps the access token in memory only', () => {
    store.setSession(tokens);
    expect(store.accessToken()).toBe('access-1');
    expect(sessionStorage.getItem(TokenStore.refreshStorageKey)).toBe('refresh-1');
    expect(localStorage.getItem(TokenStore.refreshStorageKey)).toBeNull();
    expect(sessionStorage.getItem('accessToken')).toBeNull();
  });

  it('clear removes memory access token and sessionStorage refresh', () => {
    store.setSession(tokens);
    store.clear();
    expect(store.accessToken()).toBeNull();
    expect(store.getRefreshToken()).toBeNull();
    expect(sessionStorage.getItem(TokenStore.refreshStorageKey)).toBeNull();
  });
});
