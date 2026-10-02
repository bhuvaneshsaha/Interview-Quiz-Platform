import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { of, Subject, switchMap } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  AnswerDto,
  AnswerValue,
  CandidateAttemptResponse,
  CandidateItemResult,
  CandidateQuestion,
  CandidateSubmitResponse,
} from '../../../core/api/contracts';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { MagicLinkApi } from '../../../core/api/magic-link-api.service';
import { CandidateSession } from '../../../core/auth/candidate-session.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { resultStatusLabel } from '../../../shared/labels/status-labels';
import { OfflineBanner } from '../../../core/pwa/offline-banner/offline-banner.component';
import { QUESTION_TYPE_LABELS } from '../../quizzes/quiz-form.mapper';
import {
  bankItems,
  boolValueOf,
  choiceOptions,
  initialAnswer,
  itemIdsOf,
  longTextGuidance,
  longTextMaxLength,
  optionIdOf,
  optionIdsOf,
  orderingItems,
  perSlots,
  sanitizeForSave,
  sharedSlots,
  slotItemId,
  slotOptionId,
  textOf,
} from './candidate-answer.util';
import { nextTimerAnnouncement, timerUrgency, TimerAnnouncementState } from './timer-urgency';

@Component({
  selector: 'app-attempt',
  imports: [FormsModule, PageStatus, OfflineBanner],
  templateUrl: './attempt.component.html',
  styleUrl: './attempt.component.css',
})
export class Attempt implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly magicLinks = inject(MagicLinkApi);
  private readonly attemptsApi = inject(AttemptsApi);
  private readonly candidate = inject(CandidateSession);
  private readonly destroyRef = inject(DestroyRef);
  private readonly saves = new Subject<void>();
  private timerId: ReturnType<typeof setInterval> | null = null;
  private autoSubmitStarted = false;
  private timerAnnounced: TimerAnnouncementState = { five: false, one: false };

  readonly typeLabels = QUESTION_TYPE_LABELS;
  readonly resultStatusLabel = resultStatusLabel;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly bannerError = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly attempt = signal<CandidateAttemptResponse | null>(null);
  readonly answers = signal<Record<string, AnswerValue | undefined>>({});
  readonly itemResults = signal<CandidateItemResult[]>([]);
  readonly submitSummary = signal<CandidateSubmitResponse | null>(null);
  readonly confirmSubmit = signal(false);
  readonly autoSubmitted = signal(false);
  readonly timerAnnouncement = signal('');
  readonly now = signal(Date.now());

  readonly remainingSeconds = computed(() => {
    const attempt = this.attempt();
    this.now();
    if (!attempt) {
      return 0;
    }
    const due = Date.parse(attempt.dueAtUtc);
    if (Number.isNaN(due)) {
      return Math.max(0, attempt.remainingSeconds);
    }
    return Math.max(0, Math.floor((due - this.now()) / 1000));
  });

  readonly submitted = computed(() => {
    const attempt = this.attempt();
    return attempt?.status === 'submitted' || this.submitSummary() !== null;
  });

  readonly timerUrgency = computed(() => {
    if (this.submitted() || this.submitting()) {
      return 'none' as const;
    }
    return timerUrgency(this.remainingSeconds());
  });

  constructor() {
    this.candidate.enterAttempt();
    this.destroyRef.onDestroy(() => {
      this.stopTimer();
      this.candidate.leaveAttempt();
    });
    this.saves.pipe(debounceTime(800), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.flushAnswers();
    });
  }

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token')?.trim() ?? '';
    if (!token) {
      this.error.set('Invite token is missing. Open the invite link you were sent.');
      this.loading.set(false);
      return;
    }
    this.magicLinks.consume(token).subscribe({
      next: (tokens) => {
        this.candidate.setSession(tokens);
        this.startAttempt(tokens.assignmentId);
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  remainingLabel(): string {
    const total = this.remainingSeconds();
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const seconds = total % 60;
    const mm = String(minutes).padStart(2, '0');
    const ss = String(seconds).padStart(2, '0');
    return hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`;
  }

  typeLabel(question: CandidateQuestion): string {
    return this.typeLabels[question.type] ?? question.type;
  }

  options = choiceOptions;
  sharedSlots = sharedSlots;
  bankItems = bankItems;
  perSlots = perSlots;
  orderingItems = orderingItems;
  longTextGuidance = longTextGuidance;
  longTextMaxLength = longTextMaxLength;
  optionIdOf = optionIdOf;
  optionIdsOf = optionIdsOf;
  boolValueOf = boolValueOf;
  textOf = textOf;
  slotItemId = slotItemId;
  slotOptionId = slotOptionId;

  answer(questionId: string): AnswerValue | undefined {
    return this.answers()[questionId];
  }

  orderedIds(question: CandidateQuestion): string[] {
    return itemIdsOf(this.answer(question.id), orderingItems(question).map((item) => item.id));
  }

  orderingItemText(question: CandidateQuestion, id: string): string {
    return orderingItems(question).find((item) => item.id === id)?.text ?? id;
  }

  resultFor(questionId: string): CandidateItemResult | undefined {
    return this.itemResults().find((item) => item.questionId === questionId);
  }

  resultLabel(result: CandidateItemResult | undefined): string {
    if (!result) {
      return '';
    }
    if (result.status === 'unsettled') {
      return 'Unsettled — waiting for review';
    }
    if (result.pointsAwarded === null || result.pointsAwarded === undefined) {
      return 'Scored';
    }
    return `Scored: ${result.pointsAwarded}`;
  }

  setSingle(question: CandidateQuestion, optionId: string): void {
    this.patchAnswer(question.id, { optionId });
  }

  toggleMulti(question: CandidateQuestion, optionId: string, event: Event): void {
    const checked = event.target instanceof HTMLInputElement ? event.target.checked : false;
    const current = new Set(optionIdsOf(this.answer(question.id)));
    if (checked) {
      current.add(optionId);
    } else {
      current.delete(optionId);
    }
    this.patchAnswer(question.id, { optionIds: [...current] });
  }

  setTrueFalse(question: CandidateQuestion, value: boolean): void {
    this.patchAnswer(question.id, { value });
  }

  setText(question: CandidateQuestion, text: string): void {
    this.patchAnswer(question.id, { text });
  }

  setSharedSlot(question: CandidateQuestion, slotId: string, itemId: string): void {
    const slots = sharedSlots(question).map((slot) => ({
      slotId: slot.id,
      itemId: slot.id === slotId ? itemId : this.slotItemId(this.answer(question.id), slot.id),
    }));
    this.patchAnswer(question.id, { slots });
  }

  setPerSlot(question: CandidateQuestion, slotId: string, optionId: string): void {
    const slots = perSlots(question).map((slot) => ({
      slotId: slot.id,
      optionId: slot.id === slotId ? optionId : this.slotOptionId(this.answer(question.id), slot.id),
    }));
    this.patchAnswer(question.id, { slots });
  }

  moveOrder(question: CandidateQuestion, index: number, delta: number): void {
    const ids = [...this.orderedIds(question)];
    const next = index + delta;
    if (next < 0 || next >= ids.length) {
      return;
    }
    const [item] = ids.splice(index, 1);
    ids.splice(next, 0, item);
    this.patchAnswer(question.id, { itemIds: ids });
  }

  requestSubmit(): void {
    const attempt = this.attempt();
    if (!attempt || this.submitted() || this.submitting()) {
      return;
    }
    this.confirmSubmit.set(true);
  }

  cancelSubmit(): void {
    this.confirmSubmit.set(false);
  }

  confirmAndSubmit(): void {
    this.confirmSubmit.set(false);
    this.submit('manual');
  }

  submit(source: 'manual' | 'timeout' = 'manual'): void {
    const attempt = this.attempt();
    if (!attempt || this.submitted() || this.submitting()) {
      return;
    }
    this.confirmSubmit.set(false);
    if (source === 'timeout') {
      this.autoSubmitted.set(true);
    }
    this.submitting.set(true);
    this.bannerError.set(null);
    const payload = this.buildSavePayload(attempt);
    const save$ = payload.length
      ? this.attemptsApi.saveAnswers(attempt.assignmentId, { answers: payload })
      : of(attempt);
    save$
      .pipe(switchMap(() => this.attemptsApi.submit(attempt.assignmentId)))
      .subscribe({
        next: (result) => {
          this.submitSummary.set(result);
          this.itemResults.set(result.itemResults ?? []);
          this.attempt.update((current) =>
            current
              ? {
                  ...current,
                  status: result.status,
                  submittedAtUtc: result.submittedAtUtc,
                  remainingSeconds: 0,
                }
              : current,
          );
          this.submitting.set(false);
          this.candidate.clear();
          this.stopTimer();
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.fail(err, false, false);
        },
      });
  }

  private startAttempt(assignmentId: string): void {
    this.attemptsApi.start(assignmentId).subscribe({
      next: (attempt) => this.applyAttempt(attempt),
      error: (err: unknown) => this.fail(err),
    });
  }

  private applyAttempt(attempt: CandidateAttemptResponse): void {
    const saved = new Map<string, AnswerValue>();
    for (const answer of attempt.answers ?? []) {
      saved.set(answer.questionId, answer.value);
    }
    const next: Record<string, AnswerValue | undefined> = {};
    for (const question of attempt.questions ?? []) {
      next[question.id] = initialAnswer(question, saved.get(question.id));
    }
    this.answers.set(next);
    this.attempt.set(attempt);
    this.itemResults.set(attempt.itemResults ?? []);
    this.loading.set(false);
    if (attempt.status === 'submitted') {
      this.stopTimer();
      this.candidate.clear();
      return;
    }
    if (this.remainingSeconds() <= 0) {
      this.autoSubmitStarted = true;
      this.submit('timeout');
      return;
    }
    this.startTimer();
    this.noteTimerThreshold();
  }

  private patchAnswer(questionId: string, value: AnswerValue): void {
    if (this.submitted()) {
      return;
    }
    this.answers.update((current) => ({ ...current, [questionId]: value }));
    this.saves.next();
  }

  private buildSavePayload(attempt: CandidateAttemptResponse): AnswerDto[] {
    const payload: AnswerDto[] = [];
    for (const question of attempt.questions) {
      const value = this.answers()[question.id];
      if (!value) {
        continue;
      }
      const sanitized = sanitizeForSave(question.type, value);
      if (sanitized) {
        payload.push({ questionId: question.id, value: sanitized });
      }
    }
    return payload;
  }

  private flushAnswers(): void {
    const attempt = this.attempt();
    if (!attempt || this.submitted() || !this.candidate.hasAccessToken()) {
      return;
    }
    const payload = this.buildSavePayload(attempt);
    if (payload.length === 0) {
      return;
    }
    this.saving.set(true);
    this.attemptsApi.saveAnswers(attempt.assignmentId, { answers: payload }).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.attempt.update((current) =>
          current
            ? { ...current, remainingSeconds: updated.remainingSeconds, status: updated.status }
            : current,
        );
        if (updated.status === 'submitted') {
          this.itemResults.set(updated.itemResults ?? []);
          this.candidate.clear();
          this.stopTimer();
        }
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.fail(err, false, false);
      },
    });
  }

  private startTimer(): void {
    this.stopTimer();
    this.now.set(Date.now());
    this.timerId = setInterval(() => {
      this.now.set(Date.now());
      this.noteTimerThreshold();
      if (!this.autoSubmitStarted && this.remainingSeconds() <= 0 && !this.submitted()) {
        this.autoSubmitStarted = true;
        this.submit('timeout');
      }
    }, 1000);
  }

  private noteTimerThreshold(): void {
    const next = nextTimerAnnouncement(this.remainingSeconds(), this.timerAnnounced);
    this.timerAnnounced = next.state;
    if (next.message) {
      this.timerAnnouncement.set(next.message);
    }
  }

  private stopTimer(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }

  private fail(err: unknown, stopLoading = true, fatal = true): void {
    const mapped = PageStatus.fromError(err);
    this.correlationId.set(mapped.correlationId);
    if (fatal) {
      this.error.set(mapped.message);
    } else {
      this.bannerError.set(mapped.message);
    }
    if (stopLoading) {
      this.loading.set(false);
    }
  }
}
