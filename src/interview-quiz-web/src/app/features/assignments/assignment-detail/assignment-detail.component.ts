import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  AssignmentResponse,
  AttemptSummaryResponse,
} from '../../../core/api/contracts';
import { AssignmentsApi } from '../../../core/api/assignments-api.service';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';

const TERMINAL_STATUSES = new Set(['submitted', 'pendingReview', 'completed']);

@Component({
  selector: 'app-assignment-detail',
  imports: [RouterLink, HasPermission, PageStatus],
  templateUrl: './assignment-detail.component.html',
  styleUrl: './assignment-detail.component.css',
})
export class AssignmentDetail implements OnInit {
  private readonly api = inject(AssignmentsApi);
  private readonly attemptsApi = inject(AttemptsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly permissions = inject(PermissionService);

  readonly codes = PermissionCodes;
  readonly canWrite = this.permissions.hasPermission(PermissionCodes.AssignmentsWrite);
  readonly canReadAttempts = this.permissions.hasPermission(PermissionCodes.AttemptsRead);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly assignment = signal<AssignmentResponse | null>(null);
  readonly attempts = signal<AttemptSummaryResponse[]>([]);
  readonly attemptsLoading = signal(false);
  readonly inviteUrl = signal<string | null>(null);
  readonly inviting = signal(false);
  readonly copied = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Assignment id is required.');
      this.loading.set(false);
      return;
    }
    this.api.get(id).subscribe({
      next: (assignment) => {
        this.assignment.set(assignment);
        this.loading.set(false);
        if (this.canReadAttempts) {
          this.loadAttempts(assignment.id);
        }
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.loading.set(false);
      },
    });
  }

  get canInvite(): boolean {
    const assignment = this.assignment();
    if (!assignment || !this.canWrite) {
      return false;
    }
    return assignment.mode === 'async' && !TERMINAL_STATUSES.has(assignment.status);
  }

  invite(): void {
    const assignment = this.assignment();
    if (!assignment || !this.canInvite) {
      return;
    }
    this.inviting.set(true);
    this.copied.set(false);
    this.api.invite(assignment.id).subscribe({
      next: (response) => {
        this.inviteUrl.set(response.inviteUrl);
        this.inviting.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.inviting.set(false);
      },
    });
  }

  async copyInvite(url: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      this.copied.set(true);
    } catch {
      this.copied.set(false);
    }
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'notStarted':
        return 'Not started';
      case 'inProgress':
        return 'In progress';
      case 'submitted':
        return 'Submitted';
      case 'pendingReview':
        return 'Pending review';
      case 'completed':
        return 'Completed';
      default:
        return status;
    }
  }

  formatDate(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }

  private loadAttempts(assignmentId: string): void {
    this.attemptsLoading.set(true);
    this.attemptsApi.listByAssignment(assignmentId, 1, 20).subscribe({
      next: (result) => {
        this.attempts.set(result.items);
        this.attemptsLoading.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.attemptsLoading.set(false);
      },
    });
  }
}
