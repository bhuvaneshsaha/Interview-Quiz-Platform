import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AssignmentSummaryResponse, OpeningResponse } from '../../../core/api/contracts';
import { AssignmentsApi } from '../../../core/api/assignments-api.service';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { isUuid } from '../../quizzes/quiz-form.mapper';

@Component({
  selector: 'app-assignment-list',
  imports: [RouterLink, ReactiveFormsModule, HasPermission, PageStatus],
  templateUrl: './assignment-list.component.html',
  styleUrl: './assignment-list.component.css',
})
export class AssignmentList implements OnInit {
  private readonly api = inject(AssignmentsApi);
  private readonly openingsApi = inject(OpeningsApi);
  private readonly permissions = inject(PermissionService);

  readonly codes = PermissionCodes;
  readonly canReadOpenings = this.permissions.hasPermission(PermissionCodes.OpeningsRead);
  readonly openingFilter = new FormControl('', { nonNullable: true });
  readonly keyword = new FormControl('', { nonNullable: true });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly assignments = signal<AssignmentSummaryResponse[]>([]);
  readonly openings = signal<OpeningResponse[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;
  readonly appliedOpeningId = signal('');
  readonly appliedKeyword = signal('');

  ngOnInit(): void {
    if (this.canReadOpenings) {
      this.openingsApi.list(1, 100).subscribe({
        next: (result) => this.openings.set(result.items),
        error: () => this.openings.set([]),
      });
    }
    this.load();
  }

  submitFilter(): void {
    const rawOpening = this.openingFilter.value.trim();
    if (rawOpening && !isUuid(rawOpening)) {
      this.error.set('Opening filter must be a valid opening id.');
      this.correlationId.set(null);
      return;
    }
    this.appliedOpeningId.set(rawOpening);
    this.appliedKeyword.set(this.keyword.value.trim());
    this.load(1);
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set(null);
    const openingId = this.appliedOpeningId();
    if (openingId && !isUuid(openingId)) {
      this.error.set('Opening filter must be a valid opening id.');
      this.correlationId.set(null);
      this.loading.set(false);
      return;
    }
    this.api
      .list(page, this.pageSize, {
        openingId: openingId || undefined,
        keyword: this.appliedKeyword() || undefined,
      })
      .subscribe({
        next: (result) => {
          this.assignments.set(result.items);
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

  statusLabel(status: string): string {
    switch (status) {
      case 'notStarted':
        return 'Not started';
      case 'inProgress':
        return 'In progress';
      case 'submitted':
        return 'Submitted';
      case 'pendingReview':
        return 'Pending review';
      case 'completed':
        return 'Completed';
      default:
        return status;
    }
  }

  modeLabel(mode: string): string {
    return mode === 'live' ? 'Live' : 'Async';
  }

  get lastPage(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }
}
