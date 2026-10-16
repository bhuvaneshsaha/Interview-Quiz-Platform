import { Component } from '@angular/core';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, Routes } from '@angular/router';
import { PermissionCodes } from '../permissions/permission-codes';
import { permissionGuard } from '../permissions/permission.guard';
import { authGuard } from './auth.guard';
import { TokenStore } from './token-store.service';

@Component({ selector: 'app-deep-link-login', template: 'login' })
class LoginPage {}

@Component({ selector: 'app-deep-link-home', template: 'home' })
class HomePage {}

@Component({ selector: 'app-deep-link-reports', template: 'reports' })
class ReportsPage {}

const tokenResponse = {
  accessToken: 'access-2',
  refreshToken: 'refresh-2',
  accessTokenExpiresAt: '2099-01-01T00:00:00Z',
  refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
  tokenType: 'Bearer',
};

const me = {
  id: 'user-1',
  email: 'admin.dev@example.com',
  permissions: [PermissionCodes.ReportsRead, PermissionCodes.UsersManage],
};

function shellRoutes(): Routes {
  return [
    { path: 'login', component: LoginPage },
    {
      path: '',
      component: HomePage,
      canActivate: [authGuard],
      children: [
        { path: '', component: HomePage },
        {
          path: 'reports',
          component: ReportsPage,
          canActivate: [permissionGuard],
          data: { permission: PermissionCodes.ReportsRead },
        },
      ],
    },
  ];
}

function sameRouteGuards(): Routes {
  return [
    { path: 'login', component: LoginPage },
    { path: '', component: HomePage },
    {
      path: 'reports',
      component: ReportsPage,
      canActivate: [authGuard, permissionGuard],
      data: { permission: PermissionCodes.ReportsRead },
    },
  ];
}

describe('deep link session restore', () => {
  afterEach(() => {
    sessionStorage.clear();
  });

  async function navigate(routes: Routes, url: string): Promise<Router> {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes),
      ],
    });
    sessionStorage.setItem(TokenStore.refreshStorageKey, 'refresh-1');
    const router = TestBed.inject(Router);
    const http = TestBed.inject(HttpTestingController);
    const pending = router.navigateByUrl(url);
    await new Promise((resolve) => setTimeout(resolve, 0));
    const seen = http.match(() => true);
    if (seen.length === 0) {
      throw new Error(`No HTTP during navigation. url=${router.url}`);
    }
    for (const request of seen) {
      if (request.request.url.endsWith('/api/auth/refresh')) {
        request.flush(tokenResponse);
      } else if (request.request.url.endsWith('/api/me')) {
        request.flush(me);
      } else {
        request.flush({});
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 0));
    const followUp = http.match(() => true);
    for (const request of followUp) {
      if (request.request.url.endsWith('/api/me')) {
        request.flush(me);
      } else if (request.request.url.endsWith('/api/auth/refresh')) {
        request.flush(tokenResponse);
      } else {
        request.flush({});
      }
    }
    await pending;
    http.verify();
    return router;
  }

  it('keeps a shell child deep link after the refresh token restores reports.read', async () => {
    const router = await navigate(shellRoutes(), '/reports');
    expect(router.url).toBe('/reports');
  });

  it('keeps a route whose auth and permission guards run together', async () => {
    const router = await navigate(sameRouteGuards(), '/reports');
    expect(router.url).toBe('/reports');
  });
});
