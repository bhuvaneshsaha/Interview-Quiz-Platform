import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PermissionCodes } from './permission-codes';
import { permissionGuard } from './permission.guard';
import { PermissionService } from './permission.service';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';

describe('permissionGuard', () => {
  let permissions: PermissionService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
    permissions = TestBed.inject(PermissionService);
    router = TestBed.inject(Router);
  });

  function run(permission: string | string[]) {
    const route = { data: { permission } } as unknown as ActivatedRouteSnapshot;
    const state = {} as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => permissionGuard(route, state));
  }

  it('allows when the user has the route permission', () => {
    permissions.set([PermissionCodes.RolesManage]);
    expect(run(PermissionCodes.RolesManage)).toBe(true);
  });

  it('redirects home when the permission is missing', () => {
    permissions.set([PermissionCodes.OpeningsRead]);
    const result = run(PermissionCodes.RolesManage);
    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toBe('/');
  });
});
