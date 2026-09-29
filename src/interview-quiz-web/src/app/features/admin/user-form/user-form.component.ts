import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { RoleResponse, UserResponse } from '../../../core/api/contracts';
import { AccessApi } from '../../../core/api/access-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-user-form',
  imports: [ReactiveFormsModule, RouterLink, HasPermission, PageStatus],
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.css',
})
export class UserForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AccessApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly permissions = inject(PermissionService);

  readonly codes = PermissionCodes;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly isNew = signal(true);
  readonly user = signal<UserResponse | null>(null);
  readonly roles = signal<RoleResponse[]>([]);
  readonly canManageRoles = this.permissions.hasPermission(PermissionCodes.RolesManage);

  readonly createForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  readonly selectedRoleIds = signal<ReadonlySet<string>>(new Set());

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || id === 'new') {
      this.isNew.set(true);
      if (this.canManageRoles) {
        this.api.listRoles().subscribe({
          next: (roles) => {
            this.roles.set(roles);
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
      } else {
        this.loading.set(false);
      }
      return;
    }
    this.isNew.set(false);
    const roles$ = this.canManageRoles ? this.api.listRoles() : of<RoleResponse[]>([]);
    forkJoin({ user: this.api.getUser(id), roles: roles$ }).subscribe({
      next: ({ user, roles }) => {
        this.user.set(user);
        this.roles.set(roles);
        this.selectedRoleIds.set(new Set(user.roleIds));
        this.loading.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.loading.set(false);
      },
    });
  }

  isRoleSelected(roleId: string): boolean {
    return this.selectedRoleIds().has(roleId);
  }

  toggleRole(roleId: string, event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }
    const next = new Set(this.selectedRoleIds());
    if (target.checked) {
      next.add(roleId);
    } else {
      next.delete(roleId);
    }
    this.selectedRoleIds.set(next);
  }

  create(): void {
    this.error.set(null);
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      document.querySelector<HTMLElement>('#user-create-form .ng-invalid')?.focus();
      return;
    }
    this.saving.set(true);
    const { email, password } = this.createForm.getRawValue();
    this.api
      .createUser({ email: email.trim(), password })
      .pipe(
        switchMap((created) => {
          if (!this.canManageRoles || this.selectedRoleIds().size === 0) {
            return of(created);
          }
          const assigns = [...this.selectedRoleIds()].map((roleId) =>
            this.api.assignRole(roleId, created.id),
          );
          return forkJoin(assigns).pipe(switchMap(() => of(created)));
        }),
      )
      .subscribe({
        next: () => void this.router.navigateByUrl('/users'),
        error: (err: unknown) => this.fail(err),
      });
  }

  setDisabled(isDisabled: boolean): void {
    const current = this.user();
    if (!current) {
      return;
    }
    this.saving.set(true);
    this.api.updateUser(current.id, { isDisabled }).subscribe({
      next: (updated) => {
        this.user.set(updated);
        this.saving.set(false);
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  saveRoles(): void {
    const current = this.user();
    if (!current || !this.canManageRoles) {
      return;
    }
    this.saving.set(true);
    const previous = new Set(current.roleIds);
    const next = this.selectedRoleIds();
    const assigns = [...next]
      .filter((id) => !previous.has(id))
      .map((roleId) => this.api.assignRole(roleId, current.id));
    const unassigns = [...previous]
      .filter((id) => !next.has(id))
      .map((roleId) => this.api.unassignRole(roleId, current.id));
    const ops = [...assigns, ...unassigns];
    const done$ = ops.length === 0 ? of(true) : forkJoin(ops).pipe(switchMap(() => of(true)));
    done$
      .pipe(switchMap(() => this.api.getUser(current.id)))
      .subscribe({
        next: (updated) => {
          this.user.set(updated);
          this.selectedRoleIds.set(new Set(updated.roleIds));
          this.saving.set(false);
        },
        error: (err: unknown) => this.fail(err),
      });
  }

  private fail(err: unknown): void {
    const mapped = PageStatus.fromError(err);
    this.error.set(mapped.message);
    this.correlationId.set(mapped.correlationId);
    this.saving.set(false);
  }
}
