import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RoleResponse } from '../../../core/api/contracts';
import { AccessApi } from '../../../core/api/access-api.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-role-list',
  imports: [RouterLink, PageStatus],
  templateUrl: './role-list.component.html',
  styleUrl: './role-list.component.css',
})
export class RoleList implements OnInit {
  private readonly api = inject(AccessApi);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly roles = signal<RoleResponse[]>([]);

  ngOnInit(): void {
    this.api.listRoles().subscribe({
      next: (roles) => {
        this.roles.set(roles);
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
}
