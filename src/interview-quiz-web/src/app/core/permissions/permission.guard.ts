import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { PermissionService } from './permission.service';

export const permissionGuard: CanActivateFn = (route, state) => {
  const permissions = inject(PermissionService);
  const auth = inject(AuthService);
  const router = inject(Router);
  const required = route.data['permission'] as string | readonly string[] | undefined;
  if (!required) {
    return true;
  }
  const codes = typeof required === 'string' ? [required] : required;
  // Wait for the refresh-token restore. Checking the in-memory set first treats a
  // reload as "no permission" and navigates away before /api/me fills it in.
  return auth.restoreSession().pipe(
    map((ok) => {
      if (!ok) {
        return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
      }
      if (permissions.hasAny(codes)) {
        return true;
      }
      return router.createUrlTree(['/']);
    }),
  );
};
