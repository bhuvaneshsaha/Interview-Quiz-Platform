import { Component, DestroyRef, computed, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  CandidatesReportResponse,
  FunnelReportResponse,
  PendingReviewReportResponse,
  QuestionsReportResponse,
  ReportFilterChip,
  ReportFilterCriteria,
  ReportResponse,
  ScoresReportResponse,
  TimeReportResponse,
} from '../../../core/api/contracts';
import { ReportsApi } from '../../../core/api/reports-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { SavedFilters } from '../../../shared/saved-filters/saved-filters.component';
import { reportCriteriaFromUnknown } from '../../../shared/saved-filters/saved-filter.criteria';
import { chipText, fieldsFor, parseChipValue, reportByRoute } from '../report-catalog';

interface ChartBar {
  label: string;
  count: number;
  x: number;
  y: number;
  height: number;
}

@Component({
  selector: 'app-report-view',
  imports: [RouterLink, PageStatus, SavedFilters],
  templateUrl: './report-view.component.html',
  styleUrl: './report-view.component.css',
})
export class ReportView implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ReportsApi);
  private readonly permissions = inject(PermissionService);
  private readonly destroyRef = inject(DestroyRef);

  readonly codes = PermissionCodes;
  readonly canReadAssignments = this.permissions.hasPermission(PermissionCodes.AssignmentsRead);
  readonly canReadAttempts = this.permissions.hasPermission(PermissionCodes.AttemptsRead);
  readonly report = signal(reportByRoute(null));
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly response = signal<ReportResponse | null>(null);
  readonly chips = signal<ReportFilterChip[]>([]);
  readonly page = signal(1);
  readonly pageSize = 20;
  readonly field = signal('openingId');
  readonly operator = signal('is');
  readonly value = signal('');
  readonly valueTo = signal('');
  readonly exporting = signal(false);
  readonly missing = signal(false);
  private requestId = 0;

  readonly fields = computed(() => fieldsFor(this.report()?.key ?? 'funnel'));
  readonly selectedField = computed(
    () => this.fields().find((item) => item.field === this.field()) ?? this.fields()[0],
  );
  readonly criteria = computed<ReportFilterCriteria>(() =>
    this.chips().length > 0 ? { filters: this.chips() } : {},
  );
  readonly funnel = computed(() =>
    this.report()?.key === 'funnel' ? (this.response() as FunnelReportResponse | null) : null,
  );
  readonly scores = computed(() =>
    this.report()?.key === 'scores' ? (this.response() as ScoresReportResponse | null) : null,
  );
  readonly time = computed(() =>
    this.report()?.key === 'time' ? (this.response() as TimeReportResponse | null) : null,
  );
  readonly questions = computed(() =>
    this.report()?.key === 'questions' ? (this.response() as QuestionsReportResponse | null) : null,
  );
  readonly candidates = computed(() =>
    this.report()?.key === 'candidates' ? (this.response() as CandidatesReportResponse | null) : null,
  );
  readonly pending = computed(() =>
    this.report()?.key === 'pendingReview' ? (this.response() as PendingReviewReportResponse | null) : null,
  );
  readonly chartBars = computed(() => this.buildBars());
  readonly chartLabel = computed(() => {
    const bars = this.chartBars();
    if (bars.length === 0) {
      return 'Report chart';
    }
    const title =
      this.report()?.key === 'funnel' ? 'Funnel chart' : this.report()?.key === 'scores' ? 'Score chart' : 'Time chart';
    return `${title}. ${bars.map((bar) => `${bar.label} ${bar.count}`).join(', ')}.`;
  });

  readonly chipText = chipText;

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const definition = reportByRoute(params.get('reportKey'));
      this.report.set(definition);
      this.missing.set(!definition);
      this.chips.set([]);
      this.page.set(1);
      this.response.set(null);
      if (!definition) {
        this.loading.set(false);
        this.error.set('That report does not exist.');
        return;
      }
      const first = fieldsFor(definition.key)[0];
      this.field.set(first.field);
      this.operator.set(first.operators[0]);
      this.value.set('');
      this.valueTo.set('');
      this.load();
    });
  }

  onField(field: string): void {
    const match = this.fields().find((item) => item.field === field) ?? this.fields()[0];
    this.field.set(match.field);
    this.operator.set(match.operators[0]);
    this.value.set('');
    this.valueTo.set('');
  }

  addChip(): void {
    const field = this.selectedField();
    const parsed = parseChipValue(field, this.operator(), this.value(), this.valueTo());
    if (parsed === null || (typeof parsed === 'number' && Number.isNaN(parsed))) {
      return;
    }
    if (typeof parsed === 'object' && 'from' in parsed && (Number.isNaN(parsed.from) || Number.isNaN(parsed.to))) {
      return;
    }
    this.chips.update((chips) => [...chips, { field: field.field, operator: this.operator(), value: parsed }]);
    this.value.set('');
    this.valueTo.set('');
    this.page.set(1);
    this.load();
  }

  removeChip(index: number): void {
    this.chips.update((chips) => chips.filter((_, chipIndex) => chipIndex !== index));
    this.page.set(1);
    this.load();
  }

  applySaved(raw: unknown): void {
    const criteria = reportCriteriaFromUnknown(raw);
    this.chips.set(criteria.filters ?? []);
    this.page.set(1);
    this.load();
  }

  previousPage(): void {
    this.page.update((page) => Math.max(1, page - 1));
    this.load();
  }

  nextPage(): void {
    this.page.update((page) => page + 1);
    this.load();
  }

  exportCsv(): void {
    const definition = this.report();
    if (!definition) {
      return;
    }
    this.exporting.set(true);
    this.api
      .exportCsv(definition.route, this.criteria())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `${definition.route}.csv`;
          link.click();
          URL.revokeObjectURL(url);
          this.exporting.set(false);
        },
        error: (error: unknown) => {
          const status = PageStatus.fromError(error);
          this.error.set(status.message);
          this.correlationId.set(status.correlationId);
          this.exporting.set(false);
        },
      });
  }

  rate(value: number): string {
    return `${(value * 100).toFixed(1)}%`;
  }

  passedLabel(passed: boolean | null | undefined): string {
    if (passed === true) {
      return 'Pass';
    }
    if (passed === false) {
      return 'Fail';
    }
    return '';
  }

  private load(): void {
    const definition = this.report();
    if (!definition) {
      return;
    }
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.error.set(null);
    this.correlationId.set(null);
    this.api
      .load(definition.route, this.criteria(), this.page(), this.pageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          if (requestId !== this.requestId) {
            return;
          }
          this.response.set(response);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          if (requestId !== this.requestId) {
            return;
          }
          const status = PageStatus.fromError(error);
          this.response.set(null);
          this.error.set(status.message);
          this.correlationId.set(status.correlationId);
          this.loading.set(false);
        },
      });
  }

  private buildBars(): ChartBar[] {
    const report = this.report();
    const response = this.response();
    if (!report || !response) {
      return [];
    }
    const points =
      report.key === 'funnel'
        ? funnelPoints(response as FunnelReportResponse)
        : 'histogram' in response
          ? (response.histogram ?? []).map((bucket) => [bucket.label, bucket.count] as const)
          : [];
    if (points.length === 0) {
      return [];
    }
    const max = Math.max(...points.map((point) => point[1]), 1);
    return points.map(([label, count], index) => {
      const height = Math.round((count / max) * 96);
      return { label, count, x: 8 + index * 52, y: 112 - height, height };
    });
  }
}

function funnelPoints(response: FunnelReportResponse): Array<readonly [string, number]> {
  const totals = response.totals;
  return [
    ['Assigned', totals?.assigned ?? 0],
    ['Appeared', totals?.appeared ?? 0],
    ['Submitted', totals?.submitted ?? 0],
    ['Completed', totals?.completed ?? 0],
    ['Pending review', totals?.pendingReview ?? 0],
  ];
}
