import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { OpeningResponse, QuizResponse } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { QuizzesApi } from '../../../core/api/quizzes-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { isUuid } from '../quiz-form.mapper';

@Component({
  selector: 'app-quiz-list',
  imports: [RouterLink, ReactiveFormsModule, HasPermission, PageStatus],
  templateUrl: './quiz-list.component.html',
  styleUrl: './quiz-list.component.css',
})
export class QuizList implements OnInit {
  private readonly api = inject(QuizzesApi);
  private readonly openingsApi = inject(OpeningsApi);
  private readonly permissions = inject(PermissionService);

  readonly codes = PermissionCodes;
  readonly canReadOpenings = this.permissions.hasPermission(PermissionCodes.OpeningsRead);
  readonly openingFilter = new FormControl('', { nonNullable: true });

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly quizzes = signal<QuizResponse[]>([]);
  readonly openings = signal<OpeningResponse[]>([]);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 20;

  ngOnInit(): void {
    if (this.canReadOpenings) {
      this.openingsApi.list(1, 100).subscribe({
        next: (result) => this.openings.set(result.items),
        error: () => this.openings.set([]),
      });
    }
    this.load();
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set(null);
    const raw = this.openingFilter.value.trim();
    if (raw && !isUuid(raw)) {
      this.error.set('Opening filter must be a valid opening id.');
      this.correlationId.set(null);
      this.loading.set(false);
      return;
    }
    const openingId = raw || undefined;
    this.api.list(page, this.pageSize, openingId).subscribe({
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
