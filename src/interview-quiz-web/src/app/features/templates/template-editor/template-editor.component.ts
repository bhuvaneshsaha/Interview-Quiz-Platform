import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TemplateResponse, TemplateVersionResponse } from '../../../core/api/contracts';
import { TemplatesApi } from '../../../core/api/templates-api.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import {
  CREDIT_MODE_LABELS,
  CREDIT_MODES,
  type QuestionDraft,
  questionToDraft,
  requiresCreditMode,
  SCORING_MODE_LABELS,
  SCORING_MODES,
  toQuestionRequest,
} from '../../quizzes/quiz-form.mapper';

@Component({
  selector: 'app-template-editor',
  imports: [ReactiveFormsModule, RouterLink, PageStatus],
  templateUrl: './template-editor.component.html',
  styleUrl: './template-editor.component.css',
})
export class TemplateEditor implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(TemplatesApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly scoringModes = SCORING_MODES;
  readonly scoringLabels = SCORING_MODE_LABELS;
  readonly creditModes = CREDIT_MODES;
  readonly creditLabels = CREDIT_MODE_LABELS;
  readonly requiresCreditMode = requiresCreditMode;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly template = signal<TemplateResponse | null>(null);

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: [''],
    expectedExperienceYears: [0, [Validators.required, Validators.min(0), Validators.max(80)]],
    changeNote: ['', [Validators.required, Validators.maxLength(500)]],
    questions: this.fb.array([]),
  });

  private templateId: string | null = null;
  private drafts: QuestionDraft[] = [];

  get questions(): FormArray {
    return this.form.controls.questions;
  }

  draftsType(index: number): QuestionDraft['type'] {
    return this.drafts[index]?.type ?? 'shortText';
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Template id is missing.');
      this.loading.set(false);
      return;
    }
    this.templateId = id;
    this.api.get(id).subscribe({
      next: (template) => {
        this.template.set(template);
        this.api.getVersion(id, template.latestVersionId).subscribe({
          next: (version) => {
            this.seed(template, version);
            this.loading.set(false);
          },
          error: (err: unknown) => this.fail(err),
        });
      },
      error: (err: unknown) => this.fail(err),
    });
  }

  save(): void {
    this.error.set(null);
    const template = this.template();
    if (!this.templateId || !template) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const questions = this.drafts.map((draft, index) => {
      const row = value.questions[index] as {
        stem: string;
        points: number;
        scoringMode: QuestionDraft['scoringMode'];
        creditMode: QuestionDraft['creditMode'];
      };
      return toQuestionRequest({
        ...draft,
        stem: row.stem,
        points: Number(row.points),
        scoringMode: row.scoringMode,
        creditMode: requiresCreditMode(draft.type) ? row.creditMode : null,
      });
    });
    this.saving.set(true);
    this.api
      .saveContent(this.templateId, {
        title: value.title.trim(),
        description: value.description.trim(),
        expectedExperienceYears: value.expectedExperienceYears,
        tags: template.tags,
        rowVersion: template.rowVersion,
        changeNote: value.changeNote.trim(),
        questions,
      })
      .subscribe({
        next: () => void this.router.navigate(['/templates', this.templateId]),
        error: (err: unknown) => {
          this.fail(err);
          this.saving.set(false);
        },
      });
  }

  private seed(template: TemplateResponse, version: TemplateVersionResponse): void {
    this.drafts = version.questions.map((question) => questionToDraft(question));
    this.form.patchValue({
      title: version.title,
      description: version.description,
      expectedExperienceYears: version.expectedExperienceYears,
    });
    this.questions.clear();
    for (const draft of this.drafts) {
      this.questions.push(
        this.fb.nonNullable.group({
          stem: [draft.stem, [Validators.required, Validators.maxLength(8000)]],
          points: [draft.points, [Validators.required, Validators.min(1), Validators.max(10000)]],
          scoringMode: [draft.scoringMode, Validators.required],
          creditMode: [draft.creditMode],
        }),
      );
    }
  }

  private fail(err: unknown): void {
    const mapped = PageStatus.fromError(err);
    this.error.set(mapped.message);
    this.correlationId.set(mapped.correlationId);
    this.loading.set(false);
  }
}
