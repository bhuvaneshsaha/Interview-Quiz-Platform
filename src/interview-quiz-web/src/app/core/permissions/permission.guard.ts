import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from './permission.service';

export const permissionGuard: CanActivateFn = (route) => {
  const permissions = inject(PermissionService);
  const router = inject(Router);
  const required = route.data['permission'] as string | readonly string[] | undefined;
  if (!required) {
    return true;
  }
  const codes = typeof required === 'string' ? [required] : required;
  if (permissions.hasAny(codes)) {
    return true;
  }
  return router.createUrlTree(['/']);
};
