import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';
import { PermissionCodes } from './core/permissions/permission-codes';
import { permissionGuard } from './core/permissions/permission.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login/login.component').then((m) => m.Login),
    canActivate: [guestGuard],
    title: 'Sign in',
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell/shell.component').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      {
        path: '',
        loadComponent: () => import('./layout/home/home.component').then((m) => m.Home),
        title: 'Home',
      },
      {
        path: 'openings',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.OpeningsRead },
        loadComponent: () =>
          import('./features/openings/opening-list/opening-list.component').then(
            (m) => m.OpeningList,
          ),
        title: 'Openings',
      },
      {
        path: 'openings/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.OpeningsWrite },
        loadComponent: () =>
          import('./features/openings/opening-form/opening-form.component').then(
            (m) => m.OpeningForm,
          ),
        title: 'Create opening',
      },
      {
        path: 'openings/:id',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.OpeningsRead },
        loadComponent: () =>
          import('./features/openings/opening-form/opening-form.component').then(
            (m) => m.OpeningForm,
          ),
        title: 'Opening',
      },
      {
        path: 'opening-fields',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.OpeningsFieldsManage },
        loadComponent: () =>
          import('./features/openings/field-definitions/field-definitions.component').then(
            (m) => m.FieldDefinitions,
          ),
        title: 'Opening field defaults',
      },
      {
        path: 'roles',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.RolesManage },
        loadComponent: () =>
          import('./features/admin/role-list/role-list.component').then((m) => m.RoleList),
        title: 'Roles',
      },
      {
        path: 'roles/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.RolesManage },
        loadComponent: () =>
          import('./features/admin/role-editor/role-editor.component').then((m) => m.RoleEditor),
        title: 'Create role',
      },
      {
        path: 'roles/:id',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.RolesManage },
        loadComponent: () =>
          import('./features/admin/role-editor/role-editor.component').then((m) => m.RoleEditor),
        title: 'Edit role',
      },
      {
        path: 'users',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.UsersManage },
        loadComponent: () =>
          import('./features/admin/user-list/user-list.component').then((m) => m.UserList),
        title: 'Users',
      },
      {
        path: 'users/new',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.UsersManage },
        loadComponent: () =>
          import('./features/admin/user-form/user-form.component').then((m) => m.UserForm),
        title: 'Create user',
      },
      {
        path: 'users/:id',
        canActivate: [permissionGuard],
        data: { permission: PermissionCodes.UsersManage },
        loadComponent: () =>
          import('./features/admin/user-form/user-form.component').then((m) => m.UserForm),
        title: 'User',
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
