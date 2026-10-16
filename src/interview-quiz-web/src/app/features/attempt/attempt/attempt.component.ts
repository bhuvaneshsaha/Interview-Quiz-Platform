import { CdkDrag, CdkDragHandle, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, computed, DestroyRef, HostListener, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { of, Subject, switchMap } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  AnswerDto,
  AnswerValue,
  CandidateAttemptPreview,
  CandidateAttemptResponse,
  CandidateItemResult,
  CandidatePreviewScoreResponse,
  CandidateQuestion,
  CandidateQuizPreviewResponse,
  CandidateSubmitResponse,
  CreateQuizRequest,
} from '../../../core/api/contracts';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { MagicLinkApi } from '../../../core/api/magic-link-api.service';
import { ApiError } from '../../../core/http/api-error';
import { pollDelayMs } from '../../assignments/live-session/poll-delay';
import { QuizzesApi } from '../../../core/api/quizzes-api.service';
import { TemplatesApi } from '../../../core/api/templates-api.service';
import { CandidateSession } from '../../../core/auth/candidate-session.service';
import { EXPIRED_INVITE_MESSAGE, REVOKED_INVITE_MESSAGE } from '../../assignments/invite-lifetime';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { itemResultLabel, resultStatusLabel } from '../../../shared/labels/status-labels';
import { OfflineBanner } from '../../../core/pwa/offline-banner/offline-banner.component';
import {
  answerHint,
  BankItemView,
  bankItems,
  candidateTypeBadge,
  boolValueOf,
  choiceOptions,
  initialAnswer,
  isQuestionAnswered,
  itemIdsOf,
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
  UNTITLED_OPTION,
} from './candidate-answer.util';
import { nextTimerAnnouncement, timerUrgency, TimerAnnouncementState } from './timer-urgency';

type PreviewKind = 'quiz' | 'draft' | 'template';

const LIVE_POLL_MS = 2000;

@Component({
  selector: 'app-attempt',
  imports: [FormsModule, PageStatus, OfflineBanner, CdkDropList, CdkDrag, CdkDragHandle],
  templateUrl: './attempt.component.html',
  styleUrl: './attempt.component.css',
})
export class Attempt implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly magicLinks = inject(MagicLinkApi);
  private readonly attemptsApi = inject(AttemptsApi);
  private readonly quizzesApi = inject(QuizzesApi);
  private readonly templatesApi = inject(TemplatesApi);
  private readonly candidate = inject(CandidateSession);
  private readonly destroyRef = inject(DestroyRef);
  private readonly saves = new Subject<void>();
  private timerId: ReturnType<typeof setInterval> | null = null;
  private livePollTimer: ReturnType<typeof setTimeout> | null = null;
  private liveFailures = 0;
  private inviteChecks = 0;
  private autoSubmitStarted = false;
  private timerAnnounced: TimerAnnouncementState = { five: false, one: false };
  private enteredCandidate = false;
  private authorPreview: CandidateQuizPreviewResponse | null = null;
  private draftRequest: CreateQuizRequest | null = null;
  private pausedRemainingSeconds = 0;

  readonly resultStatusLabel = resultStatusLabel;
  readonly loading = signal(true);
  readonly inviteBlock = signal<'expired' | 'revoked' | null>(null);
  readonly starting = signal(false);
  readonly preview = signal<CandidateAttemptPreview | null>(null);
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
  readonly previewMode = signal(false);
  readonly previewKind = signal<PreviewKind | null>(null);
  readonly paused = signal(false);
  readonly checking = signal(false);
  readonly previewScore = signal<CandidatePreviewScoreResponse | null>(null);
  readonly currentIndex = signal(0);
  readonly marked = signal<ReadonlySet<string>>(new Set());
  readonly drawerOpen = signal(false);
  readonly sessionPaused = signal(false);
  readonly reconnecting = signal(false);
  readonly liveAnnouncement = signal('');

  readonly remainingSeconds = computed(() => {
    const attempt = this.attempt();
    if (!attempt) {
      return 0;
    }
    if (this.previewMode() && this.paused()) {
      return this.pausedRemainingSeconds;
    }
    if (this.sessionPaused()) {
      return Math.max(0, attempt.remainingSeconds);
    }
    this.now();
    const due = Date.parse(attempt.dueAtUtc);
    if (Number.isNaN(due)) {
      return Math.max(0, attempt.remainingSeconds);
    }
    return Math.max(0, Math.floor((due - this.now()) / 1000));
  });

  readonly pageTitle = computed(() => {
    const block = this.inviteBlock();
    if (block === 'expired') {
      return 'This invite has expired';
    }
    if (block === 'revoked') {
      return 'This invite is no longer active';
    }
    return this.preview()?.quizTitle || 'Quiz attempt';
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
    if (!this.route.snapshot.data?.['previewKind']) {
      this.candidate.enterAttempt();
      this.enteredCandidate = true;
    }
    this.destroyRef.onDestroy(() => {
      this.stopTimer();
      this.clearLivePoll();
      if (this.enteredCandidate) {
        this.candidate.leaveAttempt();
      }
    });
    this.saves.pipe(debounceTime(800), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.flushAnswers();
    });
  }

  ngOnInit(): void {
    const kind = this.route.snapshot.data?.['previewKind'] as PreviewKind | undefined;
    if (kind === 'quiz' || kind === 'draft' || kind === 'template') {
      this.previewMode.set(true);
      this.previewKind.set(kind);
      this.loadAuthorPreview(kind);
      return;
    }
    const token = this.route.snapshot.queryParamMap.get('token')?.trim() ?? '';
    if (!token) {
      this.error.set('Invite token is missing. Open the invite link you were sent.');
      this.loading.set(false);
      return;
    }
    this.candidate.setInviteToken(token);
    this.magicLinks.consume(token).subscribe({
      next: (tokens) => {
        this.candidate.setSession(tokens);
        this.loadPreview(tokens.assignmentId);
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

  answerHint = answerHint;
  candidateTypeBadge = candidateTypeBadge;
  options = choiceOptions;
  sharedSlots = sharedSlots;
  bankItems = bankItems;
  perSlots = perSlots;
  orderingItems = orderingItems;
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

  isAnswered(question: CandidateQuestion): boolean {
    return isQuestionAnswered(question, this.answer(question.id));
  }

  isMarked(questionId: string): boolean {
    return this.marked().has(questionId);
  }

  answeredCount(): number {
    const attempt = this.attempt();
    if (!attempt) {
      return 0;
    }
    return attempt.questions.filter((question) => this.isAnswered(question)).length;
  }

  unansweredCount(): number {
    const attempt = this.attempt();
    return attempt ? Math.max(0, attempt.questions.length - this.answeredCount()) : 0;
  }

  markedCount(): number {
    return this.marked().size;
  }

  questionStatus(question: CandidateQuestion, index: number): string {
    const parts = [`Question ${index + 1}`];
    parts.push(this.isAnswered(question) ? 'answered' : 'unanswered');
    if (this.isMarked(question.id)) {
      parts.push('marked for review');
    }
    if (index === this.currentIndex()) {
      parts.push('current');
    }
    return parts.join(', ');
  }

  goTo(index: number): void {
    const questions = this.attempt()?.questions ?? [];
    if (index < 0 || index >= questions.length) {
      return;
    }
    if (index !== this.currentIndex()) {
      this.flushAnswers();
      this.currentIndex.set(index);
      this.reportPosition(index);
      queueMicrotask(() => document.getElementById('question-heading')?.focus());
    }
    this.drawerOpen.set(false);
  }

  toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  toggleMark(): void {
    const attempt = this.attempt();
    const question = attempt?.questions[this.currentIndex()];
    if (!attempt || !question || this.submitted() || (this.sessionPaused() && !this.previewMode())) {
      return;
    }
    const next = new Set(this.marked());
    if (next.has(question.id)) {
      next.delete(question.id);
    } else {
      next.add(question.id);
    }
    this.marked.set(next);
    if (this.previewMode()) {
      return;
    }
    const flags = attempt.questions.map((item) => ({
      questionId: item.id,
      flagged: next.has(item.id),
    }));
    this.attemptsApi.setFlags(attempt.assignmentId, flags).subscribe({
      next: (updated) => this.applyLiveUpdate(updated),
      error: (err: unknown) => this.fail(err, false, false),
    });
  }

  onAttemptKeydown(event: KeyboardEvent): void {
    if (!this.attempt() || this.submitted() || this.confirmSubmit()) {
      return;
    }
    const target = event.target;
    if (
      target instanceof HTMLInputElement ||
      target instanceof HTMLTextAreaElement ||
      target instanceof HTMLSelectElement ||
      (target instanceof HTMLElement && target.isContentEditable)
    ) {
      return;
    }
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.goTo(this.currentIndex() + 1);
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.goTo(this.currentIndex() - 1);
    }
  }

  togglePause(): void {
    if (!this.previewMode() || this.submitted() || !this.attempt()) {
      return;
    }
    if (this.paused()) {
      const due = Date.now() + this.pausedRemainingSeconds * 1000;
      this.attempt.update((current) =>
        current ? { ...current, dueAtUtc: new Date(due).toISOString() } : current,
      );
      this.paused.set(false);
      this.startTimer();
      return;
    }
    this.pausedRemainingSeconds = this.remainingSeconds();
    this.stopTimer();
    this.paused.set(true);
  }

  checkAnswers(): void {
    const attempt = this.attempt();
    if (!attempt || !this.previewMode() || this.checking()) {
      return;
    }
    const answers = this.buildSavePayload(attempt);
    const kind = this.previewKind();
    const quizId = this.route.snapshot.paramMap.get('id');
    const versionId = this.route.snapshot.paramMap.get('versionId');
    const request$ =
      kind === 'draft' && this.draftRequest
        ? this.quizzesApi.scoreDraftPreview(this.draftRequest, answers)
        : kind === 'template' && quizId && versionId
          ? this.templatesApi.scorePreview(quizId, versionId, answers)
          : kind === 'quiz' && quizId
            ? this.quizzesApi.scorePreview(quizId, answers)
            : null;
    if (!request$) {
      this.bannerError.set('Check answers is available after the quiz is saved.');
      return;
    }
    this.checking.set(true);
    this.bannerError.set(null);
    request$.subscribe({
      next: (score) => {
        this.previewScore.set(score);
        this.itemResults.set(
          score.items.map((item) => ({
            questionId: item.questionId,
            status: item.status,
            pointsAwarded: item.pointsAwarded,
          })),
        );
        this.checking.set(false);
      },
      error: (err: unknown) => {
        this.checking.set(false);
        this.fail(err, false, false);
      },
    });
  }

  orderedIds(question: CandidateQuestion): string[] {
    return itemIdsOf(this.answer(question.id), orderingItems(question).map((item) => item.id));
  }

  orderingItemText(question: CandidateQuestion, id: string): string {
    return orderingItems(question).find((item) => item.id === id)?.text ?? UNTITLED_OPTION;
  }

  resultFor(questionId: string): CandidateItemResult | undefined {
    return this.itemResults().find((item) => item.questionId === questionId);
  }

  resultLabel(result: CandidateItemResult | undefined): string {
    if (!result) {
      return '';
    }
    return itemResultLabel(result.status, result.pointsAwarded);
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

  bankListId(question: CandidateQuestion): string {
    return `bank-${question.id}`;
  }

  slotListId(question: CandidateQuestion, slotId: string): string {
    return `slot-${question.id}-${slotId}`;
  }

  sharedConnections(question: CandidateQuestion): string[] {
    return [
      this.bankListId(question),
      ...sharedSlots(question).map((slot) => this.slotListId(question, slot.id)),
    ];
  }

  unassignedBank(question: CandidateQuestion): BankItemView[] {
    const used = new Set(
      sharedSlots(question)
        .map((slot) => this.slotItemId(this.answer(question.id), slot.id))
        .filter((id) => id.length > 0),
    );
    return bankItems(question).filter((item) => !used.has(item.id));
  }

  assignedBankItem(question: CandidateQuestion, slotId: string): BankItemView | null {
    const id = this.slotItemId(this.answer(question.id), slotId);
    return bankItems(question).find((item) => item.id === id) ?? null;
  }

  dropShared(
    question: CandidateQuestion,
    event: { item: { data: unknown } },
    slotId: string | null,
  ): void {
    const itemId = typeof event.item.data === 'string' ? event.item.data : '';
    if (!itemId || this.submitted() || this.sessionPaused()) {
      return;
    }
    const slots = sharedSlots(question).map((slot) => {
      const current = this.slotItemId(this.answer(question.id), slot.id);
      if (slotId === null) {
        return { slotId: slot.id, itemId: current === itemId ? '' : current };
      }
      if (slot.id === slotId) {
        return { slotId: slot.id, itemId };
      }
      return { slotId: slot.id, itemId: current === itemId ? '' : current };
    });
    this.patchAnswer(question.id, { slots });
  }

  exitPreview(): void {
    if (!this.previewMode()) {
      return;
    }
    void this.router.navigateByUrl(this.previewReturnUrl());
  }

  @HostListener('document:keydown.escape')
  onPreviewEscape(): void {
    if (!this.previewMode() || this.confirmSubmit()) {
      return;
    }
    this.exitPreview();
  }

  dropOrder(question: CandidateQuestion, event: { previousIndex: number; currentIndex: number }): void {
    if (this.submitted() || event.previousIndex === event.currentIndex) {
      return;
    }
    const ids = [...this.orderedIds(question)];
    moveItemInArray(ids, event.previousIndex, event.currentIndex);
    this.patchAnswer(question.id, { itemIds: ids });
  }

  setPerSlot(question: CandidateQuestion, slotId: string, optionId: string): void {
    const slots = perSlots(question).map((slot) => ({
      slotId: slot.id,
      optionId: slot.id === slotId ? optionId : this.slotOptionId(this.answer(question.id), slot.id),
    }));
    this.patchAnswer(question.id, { slots });
  }

  private previewReturnUrl(): string {
    const state = history.state as { returnUrl?: unknown } | null;
    const raw = typeof state?.returnUrl === 'string' ? state.returnUrl : '';
    if (raw.startsWith('/') && !raw.startsWith('//')) {
      return raw;
    }
    const id = this.route.snapshot.paramMap.get('id');
    if (this.previewKind() === 'template' && id) {
      return `/templates/${id}`;
    }
    if (this.previewKind() === 'quiz' && id) {
      return `/quizzes/${id}`;
    }
    return '/quizzes/new';
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
    if (this.previewMode()) {
      this.confirmSubmit.set(false);
      this.stopTimer();
      if (source === 'timeout') {
        this.autoSubmitted.set(true);
        this.attempt.update((current) =>
          current ? { ...current, status: 'submitted', remainingSeconds: 0 } : current,
        );
      }
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

  beginQuiz(): void {
    if (this.previewMode()) {
      this.startPreviewAttempt();
      return;
    }
    const info = this.preview();
    const assignmentId = this.candidate.assignmentId();
    if (!info || info.attemptStatus !== 'notStarted' || !assignmentId || this.starting()) {
      return;
    }
    this.starting.set(true);
    this.bannerError.set(null);
    this.attemptsApi.start(assignmentId).subscribe({
      next: (attempt) => {
        this.starting.set(false);
        this.applyAttempt(attempt);
      },
      error: (err: unknown) => {
        this.starting.set(false);
        this.fail(err, false, false);
      },
    });
  }

  private loadAuthorPreview(kind: PreviewKind): void {
    if (kind === 'draft') {
      const state = history.state as { draft?: CreateQuizRequest; openingName?: string } | null;
      const draft = state?.draft;
      if (!draft) {
        this.error.set('Nothing to preview. Open preview from the quiz editor.');
        this.loading.set(false);
        return;
      }
      this.draftRequest = draft;
      this.quizzesApi.previewDraft(draft).subscribe({
        next: (quiz) => {
          if (state?.openingName && !quiz.openingName) {
            quiz.openingName = state.openingName;
          }
          this.showAuthorPreview(quiz);
        },
        error: (err: unknown) => this.fail(err),
      });
      return;
    }
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Quiz preview is missing an id.');
      this.loading.set(false);
      return;
    }
    if (kind === 'template') {
      const versionId = this.route.snapshot.paramMap.get('versionId');
      if (!versionId) {
        this.error.set('Template preview is missing a version.');
        this.loading.set(false);
        return;
      }
      this.templatesApi.previewVersion(id, versionId).subscribe({
        next: (quiz) => this.showAuthorPreview(quiz),
        error: (err: unknown) => this.fail(err),
      });
      return;
    }
    this.quizzesApi.preview(id).subscribe({
      next: (quiz) => this.showAuthorPreview(quiz),
      error: (err: unknown) => this.fail(err),
    });
  }

  private showAuthorPreview(quiz: CandidateQuizPreviewResponse): void {
    this.authorPreview = quiz;
    this.preview.set({
      attemptStatus: 'notStarted',
      quizTitle: quiz.title,
      openingName: quiz.openingName || 'Preview',
      durationMinutes: quiz.suggestedDurationMinutes,
      questionCount: quiz.questionCount,
    });
    this.loading.set(false);
  }

  private startPreviewAttempt(): void {
    const quiz = this.authorPreview;
    if (!quiz || this.attempt()) {
      return;
    }
    const started = new Date();
    const due = new Date(started.getTime() + quiz.suggestedDurationMinutes * 60_000);
    this.applyAttempt({
      id: 'preview',
      assignmentId: 'preview',
      status: 'inProgress',
      startedAtUtc: started.toISOString(),
      dueAtUtc: due.toISOString(),
      submittedAtUtc: null,
      remainingSeconds: quiz.suggestedDurationMinutes * 60,
      questions: quiz.questions,
      answers: [],
    });
  }

  private loadPreview(assignmentId: string): void {
    this.attemptsApi.preview(assignmentId).subscribe({
      next: (preview) => {
        this.preview.set(preview);
        if (preview.attemptStatus === 'notStarted') {
          this.loading.set(false);
          if (preview.liveState === 'waiting') {
            this.scheduleLivePoll(LIVE_POLL_MS);
          }
          return;
        }
        this.loadCurrent(assignmentId);
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  private loadCurrent(assignmentId: string): void {
    this.attemptsApi.getCurrent(assignmentId).subscribe({
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
    this.marked.set(new Set(attempt.flaggedQuestionIds ?? []));
    this.currentIndex.set(0);
    this.sessionPaused.set(attempt.paused === true || attempt.liveState === 'paused');
    this.itemResults.set(attempt.itemResults ?? []);
    this.loading.set(false);
    if (attempt.status === 'submitted' || attempt.liveState === 'ended') {
      this.noteLiveEnd(attempt);
      this.stopTimer();
      this.candidate.clear();
      return;
    }
    if (!this.sessionPaused() && this.remainingSeconds() <= 0) {
      this.autoSubmitStarted = true;
      this.submit('timeout');
      return;
    }
    this.startTimer();
    this.noteTimerThreshold();
    if (attempt.liveState) {
      this.scheduleLivePoll(LIVE_POLL_MS);
    }
  }

  private applyLiveUpdate(updated: CandidateAttemptResponse): void {
    const wasPaused = this.sessionPaused();
    this.attempt.update((current) =>
      current
        ? {
            ...current,
            status: updated.status,
            remainingSeconds: updated.remainingSeconds,
            dueAtUtc: updated.dueAtUtc,
            submittedAtUtc: updated.submittedAtUtc,
            liveState: updated.liveState,
            paused: updated.paused,
            liveEndReason: updated.liveEndReason,
            flaggedQuestionIds: updated.flaggedQuestionIds ?? current.flaggedQuestionIds,
          }
        : current,
    );
    if (updated.flaggedQuestionIds) {
      this.marked.set(new Set(updated.flaggedQuestionIds));
    }
    const nowPaused = updated.paused === true || updated.liveState === 'paused';
    this.sessionPaused.set(nowPaused);
    if (!wasPaused && nowPaused) {
      this.liveAnnouncement.set('Paused by interviewer');
    } else if (wasPaused && !nowPaused && updated.status !== 'submitted') {
      this.liveAnnouncement.set('The interviewer resumed the quiz.');
    }
    if (updated.status === 'submitted' || updated.liveState === 'ended') {
      this.noteLiveEnd(updated);
      this.itemResults.set(updated.itemResults ?? this.itemResults());
      this.stopTimer();
      this.candidate.clear();
    }
  }

  private noteLiveEnd(updated: CandidateAttemptResponse): void {
    if (updated.liveEndReason === 'timeout') {
      this.autoSubmitted.set(true);
    }
  }

  liveEndedMessage(): string | null {
    const reason = this.attempt()?.liveEndReason;
    if (reason === 'host') {
      return 'The interviewer ended the session.';
    }
    if (reason === 'pauseLimit') {
      return 'The session ended because the pause limit was reached.';
    }
    return null;
  }

  private patchAnswer(questionId: string, value: AnswerValue): void {
    if (this.submitted()) {
      return;
    }
    this.answers.update((current) => ({ ...current, [questionId]: value }));
    if (!this.previewMode()) {
      this.saves.next();
    }
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
        if (this.isPausedConflict(err)) {
          this.sessionPaused.set(true);
          this.liveAnnouncement.set('Paused by interviewer');
          this.bannerError.set(null);
          return;
        }
        this.fail(err, false, false);
      },
    });
  }

  private startTimer(): void {
    this.stopTimer();
    this.now.set(Date.now());
    this.inviteChecks = 0;
    this.timerId = setInterval(() => {
      this.now.set(Date.now());
      this.noteTimerThreshold();
      this.inviteChecks += 1;
      if (this.inviteChecks % 2 === 0) {
        this.watchRevoke();
      }
      if (
        !this.autoSubmitStarted &&
        !this.sessionPaused() &&
        this.remainingSeconds() <= 0 &&
        !this.submitted()
      ) {
        this.autoSubmitStarted = true;
        this.submit('timeout');
      }
    }, 1000);
  }

  private watchRevoke(): void {
    const attempt = this.attempt();
    if (!attempt || this.previewMode() || this.submitted() || this.inviteBlock() || attempt.liveState) {
      return;
    }
    this.attemptsApi.getCurrent(attempt.assignmentId).subscribe({
      next: () => undefined,
      error: (err: unknown) => this.fail(err),
    });
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

  private reportPosition(index: number): void {
    const attempt = this.attempt();
    if (!attempt || this.previewMode() || !attempt.liveState || this.submitted()) {
      return;
    }
    this.attemptsApi.reportPosition(attempt.assignmentId, index).subscribe({
      error: () => undefined,
    });
  }

  private scheduleLivePoll(delayMs: number): void {
    this.clearLivePoll();
    this.livePollTimer = setTimeout(() => this.pollLive(), delayMs);
  }

  private clearLivePoll(): void {
    if (this.livePollTimer) {
      clearTimeout(this.livePollTimer);
      this.livePollTimer = null;
    }
  }

  private pollLive(): void {
    if (this.submitted() || this.inviteBlock() || this.previewMode()) {
      return;
    }
    const assignmentId = this.candidate.assignmentId();
    if (!assignmentId) {
      return;
    }
    if (!this.attempt()) {
      this.attemptsApi.preview(assignmentId).subscribe({
        next: (preview) => {
          this.liveFailures = 0;
          this.reconnecting.set(false);
          this.preview.set(preview);
          if (preview.attemptStatus === 'notStarted') {
            this.scheduleLivePoll(LIVE_POLL_MS);
            return;
          }
          this.loadCurrent(assignmentId);
        },
        error: (err: unknown) => this.onLivePollError(err),
      });
      return;
    }
    this.attemptsApi.getCurrent(assignmentId).subscribe({
      next: (updated) => {
        this.liveFailures = 0;
        this.reconnecting.set(false);
        this.applyLiveUpdate(updated);
        if (!this.submitted()) {
          this.scheduleLivePoll(LIVE_POLL_MS);
        }
      },
      error: (err: unknown) => this.onLivePollError(err),
    });
  }

  private onLivePollError(err: unknown): void {
    if (err instanceof ApiError && err.status === 401) {
      this.clearLivePoll();
      this.fail(err);
      return;
    }
    this.liveFailures += 1;
    if (this.liveFailures >= 2) {
      this.reconnecting.set(true);
    }
    this.scheduleLivePoll(pollDelayMs(this.liveFailures, LIVE_POLL_MS));
  }

  private isPausedConflict(err: unknown): boolean {
    return err instanceof ApiError && err.status === 409 && err.message === 'Session is paused.';
  }

  private fail(err: unknown, stopLoading = true, fatal = true): void {
    const mapped = PageStatus.fromError(err);
    if (fatal && err instanceof ApiError && err.status === 401) {
      this.error.set('Reopen your link.');
      this.bannerError.set(null);
      this.loading.set(false);
      this.clearLivePoll();
      return;
    }
    if (mapped.message === REVOKED_INVITE_MESSAGE) {
      this.inviteBlock.set('revoked');
      this.attempt.set(null);
      this.preview.set(null);
      this.answers.set({});
      this.submitSummary.set(null);
      this.error.set(null);
      this.bannerError.set(null);
      this.loading.set(false);
      this.saving.set(false);
      this.submitting.set(false);
      this.stopTimer();
      this.candidate.clear();
      return;
    }
    if (fatal && mapped.message === EXPIRED_INVITE_MESSAGE) {
      this.inviteBlock.set('expired');
      this.error.set(null);
      this.loading.set(false);
      return;
    }
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
