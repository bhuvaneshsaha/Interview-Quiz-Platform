import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AssignmentResponse, LiveProgressResponse } from '../../../core/api/contracts';
import { AssignmentsApi } from '../../../core/api/assignments-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { pollDelayMs } from './poll-delay';

const HOST_POLL_MS = 3000;

@Component({
  selector: 'app-live-session',
  imports: [RouterLink, HasPermission, PageStatus],
  templateUrl: './live-session.component.html',
  styleUrl: './live-session.component.css',
})
export class LiveSession implements OnInit {
  private readonly api = inject(AssignmentsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private pollTimer: ReturnType<typeof setTimeout> | null = null;
  private failures = 0;
  private assignmentId = '';

  readonly codes = PermissionCodes;
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly busy = signal(false);
  readonly confirmEnd = signal(false);
  readonly reconnecting = signal(false);
  readonly assignment = signal<AssignmentResponse | null>(null);
  readonly progress = signal<LiveProgressResponse | null>(null);

  constructor() {
    this.destroyRef.onDestroy(() => this.clearPoll());
  }

  assignmentIdLink(): string {
    return this.assignmentId;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Assignment id is required.');
      this.loading.set(false);
      return;
    }
    this.assignmentId = id;
    this.api.get(id).subscribe({
      next: (assignment) => this.assignment.set(assignment),
      error: () => this.assignment.set(null),
    });
    this.refresh(true);
  }

  stateLabel(): string {
    switch (this.progress()?.state) {
      case 'created':
        return 'Created';
      case 'lobby':
        return 'Lobby';
      case 'running':
        return 'Running';
      case 'paused':
        return 'Paused';
      case 'ended':
        return 'Ended';
      case 'submitted':
        return 'Submitted';
      default:
        return this.progress()?.state || 'Unknown';
    }
  }

  connectedLabel(): string {
    const progress = this.progress();
    if (!progress) {
      return 'No';
    }
    if (!progress.lastActivityAtUtc) {
      return progress.candidateConnected ? 'Yes' : 'Not connected yet';
    }
    const seen = this.formatWhen(progress.lastActivityAtUtc);
    return progress.candidateConnected ? `Yes, last seen ${seen}` : `No, last seen ${seen}`;
  }

  indexLabel(): string {
    const progress = this.progress();
    if (!progress || progress.currentQuestionIndex === null || progress.currentQuestionIndex === undefined) {
      return `— of ${progress?.questionCount ?? 0}`;
    }
    return `${progress.currentQuestionIndex + 1} of ${progress.questionCount}`;
  }

  remainingLabel(): string {
    const seconds = this.progress()?.remainingSeconds;
    if (seconds === null || seconds === undefined) {
      return '—';
    }
    const total = Math.max(0, seconds);
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const secs = total % 60;
    const mm = String(minutes).padStart(2, '0');
    const ss = String(secs).padStart(2, '0');
    return hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`;
  }

  activityLabel(): string {
    const at = this.progress()?.lastActivityAtUtc;
    return at ? this.formatWhen(at) : '—';
  }

  canStart(): boolean {
    const progress = this.progress();
    return !!progress && progress.state === 'lobby' && progress.candidateConnected && !this.busy();
  }

  canPause(): boolean {
    return this.progress()?.state === 'running' && !this.busy();
  }

  canResume(): boolean {
    return this.progress()?.state === 'paused' && !this.busy();
  }

  canEnd(): boolean {
    const state = this.progress()?.state;
    return (state === 'running' || state === 'paused') && !this.busy();
  }

  start(): void {
    this.runAction((id) => this.api.startLive(id));
  }

  pause(): void {
    this.runAction((id) => this.api.pauseLive(id));
  }

  resume(): void {
    this.runAction((id) => this.api.resumeLive(id));
  }

  requestEnd(): void {
    if (!this.canEnd()) {
      return;
    }
    this.confirmEnd.set(true);
  }

  cancelEnd(): void {
    this.confirmEnd.set(false);
  }

  confirmEndSession(): void {
    this.confirmEnd.set(false);
    this.runAction((id) => this.api.endLive(id));
  }

  private runAction(action: (id: string) => ReturnType<AssignmentsApi['startLive']>): void {
    if (!this.assignmentId || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.actionError.set(null);
    action(this.assignmentId).subscribe({
      next: (progress) => {
        this.progress.set(progress);
        this.busy.set(false);
        this.failures = 0;
        this.reconnecting.set(false);
        this.schedule(HOST_POLL_MS);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.actionError.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.busy.set(false);
        this.refresh(false);
      },
    });
  }

  private refresh(initial: boolean): void {
    this.api.liveProgress(this.assignmentId).subscribe({
      next: (progress) => {
        this.progress.set(progress);
        this.failures = 0;
        this.reconnecting.set(false);
        if (initial) {
          this.loading.set(false);
        }
        if (progress.state !== 'submitted' && progress.state !== 'ended') {
          this.schedule(HOST_POLL_MS);
        } else {
          this.clearPoll();
        }
      },
      error: (err: unknown) => {
        if (initial && !this.progress()) {
          const mapped = PageStatus.fromError(err);
          this.error.set(mapped.message);
          this.correlationId.set(mapped.correlationId);
          this.loading.set(false);
          return;
        }
        this.failures += 1;
        if (this.failures >= 2) {
          this.reconnecting.set(true);
        }
        this.schedule(pollDelayMs(this.failures, HOST_POLL_MS));
      },
    });
  }

  private schedule(delayMs: number): void {
    this.clearPoll();
    this.pollTimer = setTimeout(() => this.refresh(false), delayMs);
  }

  private clearPoll(): void {
    if (this.pollTimer) {
      clearTimeout(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private formatWhen(value: string): string {
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString();
  }
}
