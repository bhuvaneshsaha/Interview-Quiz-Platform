import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AttemptResultResponse, QuestionType } from '../../../core/api/contracts';
import { AttemptsApi } from '../../../core/api/attempts-api.service';
import { QUESTION_TYPE_LABELS } from '../../quizzes/quiz-form.mapper';
import { formatCandidateAnswer } from '../format-candidate-answer';
import { itemResultLabel, resultStatusLabel, statusLabel } from '../../../shared/labels/status-labels';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-attempt-detail',
  imports: [RouterLink, PageStatus],
  templateUrl: './attempt-detail.component.html',
  styleUrl: './attempt-detail.component.css',
})
export class AttemptDetail implements OnInit {
  private readonly api = inject(AttemptsApi);
  private readonly route = inject(ActivatedRoute);

  readonly statusLabel = statusLabel;
  readonly resultStatusLabel = resultStatusLabel;
  readonly itemResultLabel = itemResultLabel;
  readonly formatCandidateAnswer = formatCandidateAnswer;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly result = signal<AttemptResultResponse | null>(null);
  readonly assignmentId = signal(this.route.snapshot.paramMap.get('assignmentId') ?? '');
  readonly showEmpty = computed(() => {
    if (this.loading() || this.error()) {
      return false;
    }
    const current = this.result();
    return !current || current.items.length === 0;
  });
  readonly emptyMessage = computed(() =>
    this.result() ? 'No question results for this attempt.' : 'Attempt not found.',
  );

  ngOnInit(): void {
    const attemptId = this.route.snapshot.paramMap.get('attemptId');
    if (!attemptId) {
      this.error.set('Attempt id is required.');
      this.loading.set(false);
      return;
    }
    this.api.get(attemptId).subscribe({
      next: (result) => {
        this.result.set(result);
        if (result.assignmentId) {
          this.assignmentId.set(result.assignmentId);
        }
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

  typeLabel(type: string): string {
    return QUESTION_TYPE_LABELS[type as QuestionType] ?? statusLabel(type);
  }

  formatDate(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }
}
