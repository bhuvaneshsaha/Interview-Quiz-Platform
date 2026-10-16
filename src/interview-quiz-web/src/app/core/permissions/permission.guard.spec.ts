import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom, Observable, of } from 'rxjs';
import { PermissionCodes } from './permission-codes';
import { permissionGuard } from './permission.guard';
import { PermissionService } from './permission.service';
import { AuthService } from '../auth/auth.service';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';

describe('permissionGuard', () => {
  let permissions: PermissionService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: () => true,
            restoreSession: () => of(true),
          },
        },
      ],
    });
    permissions = TestBed.inject(PermissionService);
    router = TestBed.inject(Router);
  });

  function run(permission: string | string[]) {
    const route = { data: { permission } } as unknown as ActivatedRouteSnapshot;
    const state = { url: '/reports' } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => permissionGuard(route, state)) as Observable<
      boolean | UrlTree
    >;
  }

  it('allows when the user has the route permission', async () => {
    permissions.set([PermissionCodes.RolesManage]);
    expect(await firstValueFrom(run(PermissionCodes.RolesManage))).toBe(true);
  });

  it('redirects home when the permission is missing', async () => {
    permissions.set([PermissionCodes.OpeningsRead]);
    const result = await firstValueFrom(run(PermissionCodes.RolesManage));
    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toBe('/?denied=1');
  });

  it('waits for session restore before treating an empty set as missing reports.read', async () => {
    let restore: (() => void) | undefined;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            isAuthenticated: () => true,
            restoreSession: () =>
              new Observable<boolean>((subscriber) => {
                restore = () => {
                  subscriber.next(true);
                  subscriber.complete();
                };
              }),
          },
        },
      ],
    });
    permissions = TestBed.inject(PermissionService);
    router = TestBed.inject(Router);
    const pending = firstValueFrom(run(PermissionCodes.ReportsRead));
    permissions.set([PermissionCodes.ReportsRead]);
    restore!();
    expect(await pending).toBe(true);
  });
});
