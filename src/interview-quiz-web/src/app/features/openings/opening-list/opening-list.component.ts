import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import {
  ListCriteria,
  OpeningListCriteria,
  OpeningResponse,
} from '../../../core/api/contracts';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { openingCriteriaFromUnknown } from '../../../shared/saved-filters/saved-filter.criteria';
import { SavedFilters } from '../../../shared/saved-filters/saved-filters.component';

@Component({
  selector: 'app-opening-list',
  imports: [RouterLink, ReactiveFormsModule, HasPermission, PageStatus, SavedFilters],
  templateUrl: './opening-list.component.html',
  styleUrl: './opening-list.component.css',
})
export class OpeningList implements OnInit {
  private readonly api = inject(OpeningsApi);
  readonly codes = PermissionCodes;
  readonly ownerFilter = new FormControl('', { nonNullable: true });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly openings = signal<OpeningResponse[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;
  readonly appliedCriteria = signal<OpeningListCriteria>({});

  ngOnInit(): void {
    this.load();
  }

  criteriaFromForm(): OpeningListCriteria {
    return openingCriteriaFromUnknown({
      ...this.appliedCriteria(),
      owner: this.ownerFilter.value,
    });
  }

  applySaved(raw: ListCriteria): void {
    const criteria = openingCriteriaFromUnknown(raw);
    this.ownerFilter.setValue(criteria.owner ?? '');
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
        this.openings.set(result.items);
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

  get lastPage(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }
}
