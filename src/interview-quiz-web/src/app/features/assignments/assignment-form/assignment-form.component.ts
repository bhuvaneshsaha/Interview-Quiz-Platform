import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AssignmentMode,
  AssignmentResponse,
  CreateAssignmentRequest,
  OpeningResponse,
  QuizResponse,
} from '../../../core/api/contracts';
import { AssignmentsApi } from '../../../core/api/assignments-api.service';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { QuizzesApi } from '../../../core/api/quizzes-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { INVITE_LIFETIME_NOTE } from '../invite-lifetime';
import { statusLabel } from '../../../shared/labels/status-labels';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { isUuid } from '../../quizzes/quiz-form.mapper';

@Component({
  selector: 'app-assignment-form',
  imports: [ReactiveFormsModule, RouterLink, HasPermission, PageStatus],
  templateUrl: './assignment-form.component.html',
  styleUrl: './assignment-form.component.css',
})
export class AssignmentForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AssignmentsApi);
  private readonly openingsApi = inject(OpeningsApi);
  private readonly quizzesApi = inject(QuizzesApi);
  private readonly permissions = inject(PermissionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly codes = PermissionCodes;
  readonly canWrite = this.permissions.hasPermission(PermissionCodes.AssignmentsWrite);
  readonly canReadOpenings = this.permissions.hasPermission(PermissionCodes.OpeningsRead);
  readonly canReadQuizzes = this.permissions.hasPermission(PermissionCodes.QuizzesRead);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly openings = signal<OpeningResponse[]>([]);
  readonly quizzes = signal<QuizResponse[]>([]);
  readonly created = signal<AssignmentResponse | null>(null);
  readonly copied = signal(false);
  readonly copyError = signal<string | null>(null);
  readonly statusLabel = statusLabel;
  readonly inviteLifetimeNote = INVITE_LIFETIME_NOTE;
  private previousOpeningId = '';

  readonly form = this.fb.nonNullable.group({
    openingId: ['', Validators.required],
    quizId: ['', Validators.required],
    candidateEmail: ['', [Validators.required, Validators.email]],
    mode: this.fb.nonNullable.control<AssignmentMode>('async', Validators.required),
    overallDurationMinutes: [30, [Validators.required, Validators.min(1), Validators.max(480)]],
    attemptLimit: [1, [Validators.required, Validators.min(1), Validators.max(20)]],
  });

  ngOnInit(): void {
    if (!this.canWrite) {
      this.error.set('You do not have permission to create assignments.');
      this.form.disable();
      return;
    }
    if (this.canReadOpenings) {
      this.openingsApi.list(1, 100).subscribe({
        next: (result) => this.openings.set(result.items),
        error: () => this.openings.set([]),
      });
    }
    this.form.controls.openingId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((openingId) => {
        if (this.previousOpeningId && this.previousOpeningId !== openingId) {
          this.form.controls.quizId.setValue('');
        }
        this.previousOpeningId = openingId;
        this.loadQuizzes(openingId);
      });
    this.form.controls.mode.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((mode) => {
      this.syncDurationValidators(mode);
    });
    this.syncDurationValidators(this.form.controls.mode.value);
  }

  get isLive(): boolean {
    return this.form.controls.mode.value === 'live';
  }

  submit(): void {
    this.error.set(null);
    this.copied.set(false);
    this.copyError.set(null);
    if (!this.canWrite) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      document.querySelector<HTMLElement>('#assignment-form .ng-invalid')?.focus();
      return;
    }
    const value = this.form.getRawValue();
    const openingId = value.openingId.trim();
    const quizId = value.quizId.trim();
    if (!isUuid(openingId) || !isUuid(quizId)) {
      this.error.set('Opening and quiz must be valid ids.');
      return;
    }
    this.saving.set(true);
    const body: CreateAssignmentRequest = {
      openingId,
      quizId,
      candidateEmail: value.candidateEmail.trim(),
      mode: value.mode,
      attemptLimit: Number(value.attemptLimit),
    };
    if (value.mode === 'async') {
      body.timing = { overallDurationMinutes: Number(value.overallDurationMinutes) };
    }
    this.api.create(body).subscribe({
      next: (created) => {
        this.created.set(created);
        this.saving.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.saving.set(false);
      },
    });
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

  private loadQuizzes(openingId: string): void {
    if (!this.canReadQuizzes || !openingId || !isUuid(openingId)) {
      this.quizzes.set([]);
      return;
    }
    this.quizzesApi.list(1, 100, { openingId }).subscribe({
      next: (result) => this.quizzes.set(result.items),
      error: () => this.quizzes.set([]),
    });
  }

  private syncDurationValidators(mode: AssignmentMode): void {
    const control = this.form.controls.overallDurationMinutes;
    if (mode === 'async') {
      control.setValidators([Validators.required, Validators.min(1), Validators.max(480)]);
    } else {
      control.clearValidators();
    }
    control.updateValueAndValidity({ emitEvent: false });
  }
}
