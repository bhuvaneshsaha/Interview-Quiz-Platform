import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PermissionResponse, RoleResponse } from '../../../core/api/contracts';
import { AccessApi } from '../../../core/api/access-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-role-editor',
  imports: [ReactiveFormsModule, RouterLink, PageStatus],
  templateUrl: './role-editor.component.html',
  styleUrl: './role-editor.component.css',
})
export class RoleEditor implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AccessApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly isNew = signal(true);
  readonly catalog = signal<PermissionResponse[]>([]);
  readonly selected = signal<ReadonlySet<string>>(new Set());

  readonly modules = computed(() => {
    const groups = new Map<string, PermissionResponse[]>();
    for (const permission of this.catalog()) {
      const list = groups.get(permission.module) ?? [];
      list.push(permission);
      groups.set(permission.module, list);
    }
    return [...groups.entries()].map(([module, permissions]) => ({ module, permissions }));
  });

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
  });

  private roleId: string | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.api.listPermissions().subscribe({
      next: (catalog) => {
        const editorCatalog = catalog.filter(
          (p) => p.includeInEmployeeRoleEditor && p.code !== PermissionCodes.CandidateAttemptParticipate,
        );
        this.catalog.set(editorCatalog);
        if (!id || id === 'new') {
          this.isNew.set(true);
          this.loading.set(false);
          return;
        }
        this.isNew.set(false);
        this.roleId = id;
        this.api.getRole(id).subscribe({
          next: (role) => {
            this.patch(role);
            this.loading.set(false);
          },
          error: (err: unknown) => this.fail(err),
        });
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  toggle(code: string, event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }
    const next = new Set(this.selected());
    if (target.checked) {
      next.add(code);
    } else {
      next.delete(code);
    }
    this.selected.set(next);
  }

  isSelected(code: string): boolean {
    return this.selected().has(code);
  }

  submit(): void {
    this.error.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      document.querySelector<HTMLElement>('#role-form .ng-invalid')?.focus();
      return;
    }
    this.saving.set(true);
    const value = this.form.getRawValue();
    const body = {
      name: value.name.trim(),
      description: value.description.trim() ? value.description.trim() : null,
      permissionCodes: [...this.selected()],
    };
    const request$ =
      this.isNew() || !this.roleId
        ? this.api.createRole(body)
        : this.api.updateRole(this.roleId, body);
    request$.subscribe({
      next: () => void this.router.navigateByUrl('/roles'),
      error: (err: unknown) => {
        this.fail(err);
        this.saving.set(false);
      },
    });
  }

  deleteRole(): void {
    if (!this.roleId) {
      return;
    }
    this.saving.set(true);
    this.api.deleteRole(this.roleId).subscribe({
      next: () => void this.router.navigateByUrl('/roles'),
      error: (err: unknown) => {
        this.fail(err);
        this.saving.set(false);
      },
    });
  }

  private patch(role: RoleResponse): void {
    this.form.patchValue({
      name: role.name,
      description: role.description ?? '',
    });
    this.selected.set(new Set(role.permissionCodes));
  }

  private fail(err: unknown): void {
    const mapped = PageStatus.fromError(err);
    this.error.set(mapped.message);
    this.correlationId.set(mapped.correlationId);
    this.loading.set(false);
  }
}
