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
import { INVITE_LIFETIME_NOTE } from '../invite-lifetime';
import { resultStatusLabel, statusLabel } from '../../../shared/labels/status-labels';
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
  readonly confirmRotate = signal(false);
  readonly inviteReissued = signal(false);
  readonly inviteError = signal<string | null>(null);
  readonly copyError = signal<string | null>(null);
  readonly statusLabel = statusLabel;
  readonly resultStatusLabel = resultStatusLabel;
  readonly inviteLifetimeNote = INVITE_LIFETIME_NOTE;

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

  requestInvite(): void {
    if (!this.canInvite || this.inviting()) {
      return;
    }
    if (this.inviteUrl()) {
      this.confirmRotate.set(true);
      return;
    }
    this.issueInvite(false);
  }

  cancelRotate(): void {
    this.confirmRotate.set(false);
  }

  confirmRotateInvite(): void {
    this.confirmRotate.set(false);
    this.issueInvite(true);
  }

  async copyInvite(url: string): Promise<void> {
    this.copyError.set(null);
    try {
      await navigator.clipboard.writeText(url);
      this.copied.set(true);
    } catch {
      this.copied.set(false);
      this.copyError.set('Could not copy the invite link. Select the link and copy it manually.');
    }
  }

  formatDate(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }

  private issueInvite(reissued: boolean): void {
    const assignment = this.assignment();
    if (!assignment || !this.canInvite) {
      return;
    }
    this.inviting.set(true);
    this.copied.set(false);
    this.copyError.set(null);
    this.inviteError.set(null);
    this.api.invite(assignment.id).subscribe({
      next: (response) => {
        this.inviteUrl.set(response.inviteUrl);
        this.inviteReissued.set(reissued);
        this.inviting.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.inviteError.set(mapped.message);
        this.inviting.set(false);
      },
    });
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
