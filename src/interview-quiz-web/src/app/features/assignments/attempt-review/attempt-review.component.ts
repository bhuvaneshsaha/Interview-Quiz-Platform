import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AttemptReviewItem, AttemptReviewResponse } from '../../../core/api/contracts';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { QUESTION_TYPE_LABELS } from '../../quizzes/quiz-form.mapper';
import { formatCandidateAnswer } from '../format-candidate-answer';
import { itemResultLabel, resultStatusLabel, statusLabel } from '../../../shared/labels/status-labels';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { formatAnswerKey } from './format-answer-key';

interface ScoreDraft {
  points: string;
  comment: string;
}

@Component({
  selector: 'app-attempt-review',
  imports: [FormsModule, RouterLink, PageStatus],
  templateUrl: './attempt-review.component.html',
  styleUrl: './attempt-review.component.css',
})
export class AttemptReview implements OnInit {
  private readonly api = inject(AttemptsApi);
  private readonly route = inject(ActivatedRoute);

  readonly statusLabel = statusLabel;
  readonly resultStatusLabel = resultStatusLabel;
  readonly itemResultLabel = itemResultLabel;
  readonly formatCandidateAnswer = formatCandidateAnswer;
  readonly formatAnswerKey = formatAnswerKey;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);
  readonly savingId = signal<string | null>(null);
  readonly suggestingId = signal<string | null>(null);
  readonly review = signal<AttemptReviewResponse | null>(null);
  readonly drafts = signal<Record<string, ScoreDraft>>({});
  readonly assignmentId = signal(this.route.snapshot.paramMap.get('assignmentId') ?? '');
  readonly showEmpty = computed(() => {
    if (this.loading() || this.error()) {
      return false;
    }
    const current = this.review();
    return !current || current.items.length === 0;
  });

  ngOnInit(): void {
    const attemptId = this.route.snapshot.paramMap.get('attemptId');
    if (!attemptId) {
      this.error.set('Attempt id is required.');
      this.loading.set(false);
      return;
    }
    this.api.getReview(attemptId).subscribe({
      next: (review) => this.apply(review),
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.loading.set(false);
      },
    });
  }

  typeLabel(type: string): string {
    return QUESTION_TYPE_LABELS[type as keyof typeof QUESTION_TYPE_LABELS] ?? statusLabel(type);
  }

  draft(item: AttemptReviewItem): ScoreDraft {
    return this.drafts()[item.questionId] ?? { points: '', comment: '' };
  }

  updateDraft(questionId: string, patch: Partial<ScoreDraft>): void {
    const current = this.drafts()[questionId] ?? { points: '', comment: '' };
    this.drafts.update((drafts) => ({ ...drafts, [questionId]: { ...current, ...patch } }));
  }

  suggest(item: AttemptReviewItem): void {
    const current = this.review();
    if (!current || this.suggestingId() || this.savingId()) {
      return;
    }
    this.saveError.set(null);
    this.suggestingId.set(item.questionId);
    this.api.suggestItem(current.id, item.questionId).subscribe({
      next: (review) => {
        this.apply(review);
        this.suggestingId.set(null);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.saveError.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.suggestingId.set(null);
      },
    });
  }

  useSuggestion(item: AttemptReviewItem): void {
    this.updateDraft(item.questionId, {
      points:
        item.suggestedPoints === null || item.suggestedPoints === undefined ? '' : String(item.suggestedPoints),
      comment: item.suggestionFeedback ?? '',
    });
  }

  save(item: AttemptReviewItem): void {
    const current = this.review();
    if (!current || this.savingId() || this.suggestingId()) {
      return;
    }
    const draft = this.draft(item);
    const points = Number(draft.points);
    if (!Number.isFinite(points) || points < 0 || points > item.points) {
      this.saveError.set(`Points awarded must be between 0 and ${item.points}.`);
      return;
    }
    this.saveError.set(null);
    this.savingId.set(item.questionId);
    this.api
      .scoreItem(current.id, item.questionId, {
        pointsAwarded: points,
        comment: draft.comment.trim() ? draft.comment.trim() : null,
      })
      .subscribe({
        next: (review) => {
          this.apply(review);
          this.savingId.set(null);
        },
        error: (err: unknown) => {
          const mapped = PageStatus.fromError(err);
          this.saveError.set(mapped.message);
          this.correlationId.set(mapped.correlationId);
          this.savingId.set(null);
        },
      });
  }

  private apply(review: AttemptReviewResponse): void {
    this.review.set(review);
    if (review.assignmentId) {
      this.assignmentId.set(review.assignmentId);
    }
    const drafts: Record<string, ScoreDraft> = {};
    for (const item of review.items) {
      if (!item.reviewable) {
        continue;
      }
      drafts[item.questionId] = {
        points: item.pointsAwarded === null || item.pointsAwarded === undefined ? '' : String(item.pointsAwarded),
        comment: item.reviewComment ?? '',
      };
    }
    this.drafts.set(drafts);
    this.loading.set(false);
  }
}
