import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UserResponse } from '../../../core/api/contracts';
import { AccessApi } from '../../../core/api/access-api.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-user-list',
  imports: [RouterLink, PageStatus],
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.css',
})
export class UserList implements OnInit {
  private readonly api = inject(AccessApi);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly users = signal<UserResponse[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;
  readonly showCandidates = signal(false);

  ngOnInit(): void {
    this.load();
  }

  onShowCandidates(event: Event): void {
    const target = event.target;
    this.showCandidates.set(target instanceof HTMLInputElement && target.checked);
    this.load(1);
  }

  roleLabel(user: UserResponse): string {
    if (user.isCandidate) {
      return 'Candidate';
    }
    return user.roleNames.length > 0 ? user.roleNames.join(', ') : 'No roles';
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.listUsers(page, this.pageSize, this.showCandidates()).subscribe({
      next: (result) => {
        this.users.set(result.items);
        this.page.set(result.page);
        this.totalCount.set(result.totalCount);
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

  get lastPage(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }
}
