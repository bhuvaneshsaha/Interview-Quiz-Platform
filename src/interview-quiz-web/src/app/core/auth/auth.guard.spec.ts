import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom, Observable, of } from 'rxjs';
import { AuthService } from './auth.service';
import { authGuard, guestGuard } from './auth.guard';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';

describe('authGuard', () => {
  it('allows an authenticated user', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated: () => true, restoreSession: () => of(true) } },
      ],
    });
    const result = await firstValueFrom(
      TestBed.runInInjectionContext(() =>
        authGuard({} as ActivatedRouteSnapshot, { url: '/openings' } as RouterStateSnapshot),
      ) as Observable<boolean | UrlTree>,
    );
    expect(result).toBe(true);
  });

  it('sends anonymous users to login', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated: () => false, restoreSession: () => of(false) } },
      ],
    });
    const router = TestBed.inject(Router);
    const result = await firstValueFrom(
      TestBed.runInInjectionContext(() =>
        authGuard({} as ActivatedRouteSnapshot, { url: '/openings' } as RouterStateSnapshot),
      ) as Observable<boolean | UrlTree>,
    );
    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toContain('/login');
  });
});

describe('guestGuard', () => {
  it('redirects authenticated users home', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { isAuthenticated: () => true } },
      ],
    });
    const router = TestBed.inject(Router);
    const result = TestBed.runInInjectionContext(() =>
      guestGuard({} as ActivatedRouteSnapshot, { url: '/login' } as RouterStateSnapshot),
    );
    expect(router.serializeUrl(result as UrlTree)).toBe('/');
  });
});
