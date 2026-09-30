import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  BankQuestionListCriteria,
  BankQuestionResponse,
  QuestionType,
} from '../../../core/api/contracts';
import { compactCriteria } from '../../../core/api/list-query';
import { QuestionsApi } from '../../../core/api/questions-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import {
  optionalIntFromInput,
  tagsForListForm,
} from '../../../shared/saved-filters/saved-filter.criteria';
import { QUESTION_TYPE_LABELS, QUESTION_TYPES } from '../../quizzes/quiz-form.mapper';

@Component({
  selector: 'app-question-list',
  imports: [RouterLink, ReactiveFormsModule, HasPermission, PageStatus],
  templateUrl: './question-list.component.html',
  styleUrl: './question-list.component.css',
})
export class QuestionList implements OnInit {
  private readonly api = inject(QuestionsApi);
  private readonly permissions = inject(PermissionService);

  readonly codes = PermissionCodes;
  readonly canWrite = this.permissions.hasPermission(PermissionCodes.QuestionsWrite);
  readonly types = QUESTION_TYPES;
  readonly typeLabels = QUESTION_TYPE_LABELS;

  readonly keyword = new FormControl('', { nonNullable: true });
  readonly typeFilter = new FormControl('', { nonNullable: true });
  readonly experienceMin = new FormControl('', { nonNullable: true });
  readonly experienceMax = new FormControl('', { nonNullable: true });
  readonly tagKey = new FormControl('', { nonNullable: true });
  readonly tagValue = new FormControl('', { nonNullable: true });
  readonly showArchived = new FormControl(false, { nonNullable: true });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly questions = signal<BankQuestionResponse[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;
  readonly appliedCriteria = signal<BankQuestionListCriteria>({});

  ngOnInit(): void {
    this.load();
  }

  criteriaFromForm(): BankQuestionListCriteria {
    const type = this.typeFilter.value;
    return compactCriteria({
      keyword: this.keyword.value.trim(),
      type: QUESTION_TYPES.includes(type as QuestionType) ? (type as QuestionType) : undefined,
      experienceMinYears: optionalIntFromInput(this.experienceMin.value),
      experienceMaxYears: optionalIntFromInput(this.experienceMax.value),
      tags: tagsForListForm(this.tagKey.value, this.tagValue.value, this.appliedCriteria().tags),
      archived: this.canWrite && this.showArchived.value ? true : undefined,
    }) as BankQuestionListCriteria;
  }

  submitFilter(): void {
    this.appliedCriteria.set(this.criteriaFromForm());
    this.load(1);
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.list(page, this.pageSize, this.appliedCriteria()).subscribe({
      next: (result) => {
        this.questions.set(result.items);
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

  typeLabel(type: QuestionType): string {
    return this.typeLabels[type] ?? type;
  }

  stemExcerpt(stem: string, max = 80): string {
    const trimmed = stem.trim();
    if (trimmed.length <= max) {
      return trimmed;
    }
    return `${trimmed.slice(0, max).trimEnd()}…`;
  }

  get lastPage(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }
}
