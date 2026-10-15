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

const TERMINAL_STATUSES = new Set(['pendingReview', 'completed']);

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
  readonly canReview = this.permissions.hasPermission(PermissionCodes.AttemptsReview);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly assignment = signal<AssignmentResponse | null>(null);
  readonly attempts = signal<AttemptSummaryResponse[]>([]);
  readonly attemptsLoading = signal(false);
  readonly inviteUrl = signal<string | null>(null);
  readonly inviting = signal(false);
  readonly emailBusy = signal(false);
  readonly copied = signal(false);
  readonly confirmRotate = signal(false);
  readonly confirmRevoke = signal(false);
  readonly inviteReissued = signal(false);
  readonly inviteNotice = signal<string | null>(null);
  readonly inviteError = signal<string | null>(null);
  readonly expiresAtLocal = signal('');
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
    return (assignment.mode === 'async' || assignment.mode === 'live') && !TERMINAL_STATUSES.has(assignment.status);
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

  emailStatusLabel(status: string | null | undefined): string {
    switch (status) {
      case 'sent':
        return 'Sent';
      case 'failed':
        return 'Failed';
      case 'resent':
        return 'Resent';
      default:
        return 'Not sent';
    }
  }

  inviteStateLabel(state: string | null | undefined): string {
    switch (state) {
      case 'active':
        return 'Active';
      case 'expired':
        return 'Expired';
      case 'revoked':
        return 'Revoked';
      default:
        return 'Not issued';
    }
  }

  cancelRotate(): void {
    this.confirmRotate.set(false);
  }

  requestRevoke(): void {
    if (!this.canInvite) {
      return;
    }
    this.confirmRevoke.set(true);
  }

  cancelRevoke(): void {
    this.confirmRevoke.set(false);
  }

  confirmRevokeInvite(): void {
    this.confirmRevoke.set(false);
    const assignment = this.assignment();
    if (!assignment) {
      return;
    }
    this.emailBusy.set(true);
    this.inviteError.set(null);
    this.api.revokeInvite(assignment.id).subscribe({
      next: (response) => {
        this.assignment.update((current) =>
          current ? { ...current, inviteState: response.inviteState, inviteExpiresAt: response.expiresAt ?? null } : current,
        );
        this.inviteUrl.set(null);
        this.inviteNotice.set('Invite revoked. The current link no longer works.');
        this.emailBusy.set(false);
      },
      error: (err: unknown) => this.failInvite(err),
    });
  }

  onExpiryInput(event: Event): void {
    const value = event.target instanceof HTMLInputElement ? event.target.value : '';
    this.expiresAtLocal.set(value);
  }

  extendInvite(): void {
    const assignment = this.assignment();
    const local = this.expiresAtLocal().trim();
    if (!assignment || !this.canInvite || !local) {
      this.inviteError.set('Choose a future expiry before extending.');
      return;
    }
    const parsed = new Date(local);
    if (Number.isNaN(parsed.getTime())) {
      this.inviteError.set('Invite expiry is not a valid date.');
      return;
    }
    this.emailBusy.set(true);
    this.inviteError.set(null);
    this.api.extendInvite(assignment.id, parsed.toISOString()).subscribe({
      next: (response) => {
        this.assignment.update((current) =>
          current ? { ...current, inviteState: response.inviteState, inviteExpiresAt: response.expiresAt ?? null } : current,
        );
        this.inviteNotice.set('Expiry updated. Resend the email if the candidate should be notified.');
        this.emailBusy.set(false);
      },
      error: (err: unknown) => this.failInvite(err),
    });
  }

  resendEmail(): void {
    const assignment = this.assignment();
    if (!assignment || !this.canInvite) {
      return;
    }
    this.emailBusy.set(true);
    this.inviteError.set(null);
    this.api.resendInvite(assignment.id).subscribe({
      next: (response) => {
        this.assignment.update((current) =>
          current
            ? {
                ...current,
                inviteEmailStatus: response.inviteEmailStatus,
                inviteEmailAttemptCount: response.inviteEmailAttemptCount,
                inviteEmailLastError: response.inviteEmailLastError ?? null,
              }
            : current,
        );
        this.inviteNotice.set(
          response.inviteEmailStatus === 'failed'
            ? 'Email could not be sent.'
            : 'Invite email resent. The link is unchanged.',
        );
        this.emailBusy.set(false);
      },
      error: (err: unknown) => this.failInvite(err),
    });
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
      error: (err: unknown) => this.failInvite(err),
    });
  }

  private failInvite(err: unknown): void {
    const mapped = PageStatus.fromError(err);
    this.inviteError.set(mapped.message);
    this.inviting.set(false);
    this.emailBusy.set(false);
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
