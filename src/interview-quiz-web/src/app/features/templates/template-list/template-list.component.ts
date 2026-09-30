import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ListCriteria, TemplateListCriteria, TemplateSummaryResponse } from '../../../core/api/contracts';
import { TemplatesApi } from '../../../core/api/templates-api.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import {
  firstTagPair,
  optionalIntFromInput,
  tagsForListForm,
  templateCriteriaFromUnknown,
} from '../../../shared/saved-filters/saved-filter.criteria';
import { SavedFilters } from '../../../shared/saved-filters/saved-filters.component';

@Component({
  selector: 'app-template-list',
  imports: [RouterLink, ReactiveFormsModule, PageStatus, SavedFilters],
  templateUrl: './template-list.component.html',
  styleUrl: './template-list.component.css',
})
export class TemplateList implements OnInit {
  private readonly api = inject(TemplatesApi);

  readonly keyword = new FormControl('', { nonNullable: true });
  readonly experienceMin = new FormControl('', { nonNullable: true });
  readonly experienceMax = new FormControl('', { nonNullable: true });
  readonly tagKey = new FormControl('', { nonNullable: true });
  readonly tagValue = new FormControl('', { nonNullable: true });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly templates = signal<TemplateSummaryResponse[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;
  readonly appliedCriteria = signal<TemplateListCriteria>({});

  ngOnInit(): void {
    this.load();
  }

  criteriaFromForm(): TemplateListCriteria {
    return templateCriteriaFromUnknown({
      keyword: this.keyword.value,
      experienceMinYears: optionalIntFromInput(this.experienceMin.value),
      experienceMaxYears: optionalIntFromInput(this.experienceMax.value),
      tags: tagsForListForm(this.tagKey.value, this.tagValue.value, this.appliedCriteria().tags),
    });
  }

  applySaved(raw: ListCriteria): void {
    const criteria = templateCriteriaFromUnknown(raw);
    this.keyword.setValue(criteria.keyword ?? '');
    this.experienceMin.setValue(criteria.experienceMinYears?.toString() ?? '');
    this.experienceMax.setValue(criteria.experienceMaxYears?.toString() ?? '');
    const pair = firstTagPair(criteria.tags);
    this.tagKey.setValue(pair.key);
    this.tagValue.setValue(pair.value);
    this.appliedCriteria.set(criteria);
    this.load(1);
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
        this.templates.set(result.items);
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

  formatUpdated(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }

  get lastPage(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }
}
