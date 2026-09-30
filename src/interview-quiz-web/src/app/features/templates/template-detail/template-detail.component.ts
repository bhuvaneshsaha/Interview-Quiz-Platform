import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  OpeningResponse,
  QuestionResponse,
  TemplateResponse,
  TemplateVersionResponse,
  TemplateVersionSummaryResponse,
} from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { TemplatesApi } from '../../../core/api/templates-api.service';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import { isUuid, QUESTION_TYPE_LABELS } from '../../quizzes/quiz-form.mapper';

@Component({
  selector: 'app-template-detail',
  imports: [RouterLink, ReactiveFormsModule, PageStatus],
  templateUrl: './template-detail.component.html',
  styleUrl: './template-detail.component.css',
})
export class TemplateDetail implements OnInit {
  private readonly api = inject(TemplatesApi);
  private readonly openingsApi = inject(OpeningsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly permissions = inject(PermissionService);

  readonly typeLabels = QUESTION_TYPE_LABELS;
  readonly canReadOpenings = this.permissions.hasPermission(PermissionCodes.OpeningsRead);
  readonly canClone =
    this.permissions.hasPermission(PermissionCodes.QuizzesWrite) &&
    this.permissions.hasPermission(PermissionCodes.TemplatesRead);

  readonly openingId = new FormControl('', { nonNullable: true });
  readonly cloneTitle = new FormControl('', { nonNullable: true });

  readonly loading = signal(true);
  readonly versionsLoading = signal(false);
  readonly questionsLoading = signal(false);
  readonly cloning = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly cloneError = signal<string | null>(null);
  readonly cloneCorrelationId = signal<string | null>(null);
  readonly template = signal<TemplateResponse | null>(null);
  readonly versions = signal<TemplateVersionSummaryResponse[]>([]);
  readonly versionPage = signal(1);
  readonly versionTotal = signal(0);
  readonly versionPageSize = 20;
  readonly selectedVersion = signal<TemplateVersionResponse | null>(null);
  readonly openings = signal<OpeningResponse[]>([]);

  private templateId: string | null = null;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Template id is missing.');
      this.loading.set(false);
      return;
    }
    this.templateId = id;

    if (this.canReadOpenings) {
      this.openingsApi.list(1, 100).subscribe({
        next: (result) => this.openings.set(result.items),
        error: () => this.openings.set([]),
      });
    }

    this.api.get(id).subscribe({
      next: (template) => {
        this.template.set(template);
        this.loading.set(false);
        this.loadVersions();
        this.loadVersionQuestions(template.latestVersionId);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.loading.set(false);
      },
    });
  }

  loadVersions(page = 1): void {
    if (!this.templateId) {
      return;
    }
    this.versionsLoading.set(true);
    this.api.listVersions(this.templateId, page, this.versionPageSize).subscribe({
      next: (result) => {
        this.versions.set(result.items);
        this.versionPage.set(result.page);
        this.versionTotal.set(result.totalCount);
        this.versionsLoading.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.versionsLoading.set(false);
      },
    });
  }

  loadVersionQuestions(versionId: string): void {
    if (!this.templateId) {
      return;
    }
    this.questionsLoading.set(true);
    this.api.getVersion(this.templateId, versionId).subscribe({
      next: (version) => {
        this.selectedVersion.set(version);
        this.questionsLoading.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.questionsLoading.set(false);
      },
    });
  }

  clone(): void {
    this.cloneError.set(null);
    this.cloneCorrelationId.set(null);
    if (!this.canClone || !this.templateId) {
      return;
    }
    const openingId = this.openingId.value.trim();
    if (!openingId) {
      this.cloneError.set('Opening is required.');
      return;
    }
    if (!isUuid(openingId)) {
      this.cloneError.set('Opening must be a valid id.');
      return;
    }
    const versionId = this.selectedVersion()?.id ?? this.template()?.latestVersionId;
    if (!versionId) {
      this.cloneError.set('Select a version to clone.');
      return;
    }
    const title = this.cloneTitle.value.trim();
    this.cloning.set(true);
    this.api
      .clone(this.templateId, versionId, {
        openingId,
        ...(title ? { title } : {}),
      })
      .subscribe({
        next: (quiz) => {
          void this.router.navigate(['/quizzes', quiz.id]);
        },
        error: (err: unknown) => {
          const mapped = PageStatus.fromError(err);
          this.cloneError.set(mapped.message);
          this.cloneCorrelationId.set(mapped.correlationId);
          this.cloning.set(false);
        },
      });
  }

  questionLabel(question: QuestionResponse): string {
    return this.typeLabels[question.type] ?? question.type;
  }

  tagEntries(tags: Record<string, string>): { key: string; value: string }[] {
    return Object.entries(tags).map(([key, value]) => ({ key, value }));
  }

  formatWhen(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }

  get lastVersionPage(): number {
    return Math.max(1, Math.ceil(this.versionTotal() / this.versionPageSize));
  }
}
