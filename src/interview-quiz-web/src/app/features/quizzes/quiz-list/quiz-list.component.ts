import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ListCriteria, OpeningResponse, QuizListCriteria, QuizResponse } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { QuizzesApi } from '../../../core/api/quizzes-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import {
  optionalIntFromInput,
  quizCriteriaFromUnknown,
} from '../../../shared/saved-filters/saved-filter.criteria';
import { SavedFilters } from '../../../shared/saved-filters/saved-filters.component';
import { isUuid } from '../quiz-form.mapper';

@Component({
  selector: 'app-quiz-list',
  imports: [RouterLink, ReactiveFormsModule, HasPermission, PageStatus, SavedFilters],
  templateUrl: './quiz-list.component.html',
  styleUrl: './quiz-list.component.css',
})
export class QuizList implements OnInit {
  private readonly api = inject(QuizzesApi);
  private readonly openingsApi = inject(OpeningsApi);
  private readonly permissions = inject(PermissionService);

  readonly codes = PermissionCodes;
  readonly canReadOpenings = this.permissions.hasPermission(PermissionCodes.OpeningsRead);
  readonly canWrite = computed(() => this.permissions.hasPermission(PermissionCodes.QuizzesWrite));
  readonly filtered = computed(() => Object.keys(this.appliedCriteria()).length > 0);
  readonly emptyMessage = computed(() => (this.filtered() ? 'No quizzes match this filter.' : 'No quizzes yet.'));
  readonly emptyActionLabel = computed(() => (this.canWrite() ? 'Create quiz' : null));
  readonly emptyActionLink = computed(() => (this.canWrite() ? '/quizzes/new' : null));
  readonly openingFilter = new FormControl('', { nonNullable: true });
  readonly keyword = new FormControl('', { nonNullable: true });
  readonly experienceMin = new FormControl('', { nonNullable: true });
  readonly experienceMax = new FormControl('', { nonNullable: true });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly quizzes = signal<QuizResponse[]>([]);
  readonly openings = signal<OpeningResponse[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;
  readonly appliedCriteria = signal<QuizListCriteria>({});

  ngOnInit(): void {
    if (this.canReadOpenings) {
      this.openingsApi.list(1, 100).subscribe({
        next: (result) => this.openings.set(result.items),
        error: () => this.openings.set([]),
      });
    }
    this.load();
  }

  criteriaFromForm(): QuizListCriteria {
    return quizCriteriaFromUnknown({
      ...this.appliedCriteria(),
      openingId: this.openingFilter.value,
      keyword: this.keyword.value,
      experienceMinYears: optionalIntFromInput(this.experienceMin.value),
      experienceMaxYears: optionalIntFromInput(this.experienceMax.value),
    });
  }

  applySaved(raw: ListCriteria): void {
    const criteria = quizCriteriaFromUnknown(raw);
    this.openingFilter.setValue(criteria.openingId ?? '');
    this.keyword.setValue(criteria.keyword ?? '');
    this.experienceMin.setValue(criteria.experienceMinYears?.toString() ?? '');
    this.experienceMax.setValue(criteria.experienceMaxYears?.toString() ?? '');
    this.appliedCriteria.set(criteria);
    this.load(1);
  }

  submitFilter(): void {
    const rawOpening = this.openingFilter.value.trim();
    if (rawOpening && !isUuid(rawOpening)) {
      this.error.set('Opening filter must be a valid opening id.');
      this.correlationId.set(null);
      return;
    }
    this.appliedCriteria.set(this.criteriaFromForm());
    this.load(1);
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set(null);
    const raw = this.appliedCriteria().openingId?.trim() ?? '';
    if (raw && !isUuid(raw)) {
      this.error.set('Opening filter must be a valid opening id.');
      this.correlationId.set(null);
      this.loading.set(false);
      return;
    }
    this.api.list(page, this.pageSize, this.appliedCriteria()).subscribe({
      next: (result) => {
        this.quizzes.set(result.items);
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

  openingTitle(openingId: string): string {
    return this.openings().find((opening) => opening.id === openingId)?.title ?? openingId;
  }

  formatUpdated(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }

  get lastPage(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }
}
