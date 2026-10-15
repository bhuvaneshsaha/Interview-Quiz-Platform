import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AiDraftResponse,
  CreditMode,
  OpeningResponse,
  QuestionBody,
  QuestionType,
  ScoringMode,
} from '../../../core/api/contracts';
import { AiDraftsApi } from '../../../core/api/ai-drafts-api.service';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PageStatus } from '../../../shared/page-status/page-status.component';

@Component({
  selector: 'app-ai-draft-form',
  imports: [ReactiveFormsModule, RouterLink, HasPermission, PageStatus],
  templateUrl: './ai-draft-form.component.html',
  styleUrl: './ai-draft-form.component.css',
})
export class AiDraftForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly openingsApi = inject(OpeningsApi);
  private readonly draftsApi = inject(AiDraftsApi);

  readonly codes = PermissionCodes;
  readonly openings = signal<OpeningResponse[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly draft = signal<AiDraftResponse | null>(null);
  readonly savedQuizId = signal<string | null>(null);

  readonly source = this.fb.nonNullable.group({
    openingId: ['', Validators.required],
    resumeText: [''],
    rulesText: [''],
  });

  readonly editor = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: [''],
    expectedExperienceYears: [0, [Validators.required, Validators.min(0), Validators.max(80)]],
    questions: this.fb.array<FormGroup>([]),
  });

  ngOnInit(): void {
    this.openingsApi.list(1, 100).subscribe({
      next: (page) => this.openings.set(page.items),
      error: (error: unknown) => this.showError(error),
    });
  }

  get questions(): FormArray<FormGroup> {
    return this.editor.controls.questions;
  }

  generate(): void {
    if (this.source.invalid) {
      this.source.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.savedQuizId.set(null);
    const value = this.source.getRawValue();
    this.draftsApi
      .create({
        openingId: value.openingId,
        resumeText: value.resumeText.trim() || null,
        rulesText: value.rulesText.trim() || null,
      })
      .subscribe({
        next: (draft) => {
          this.draft.set(draft);
          this.editor.controls.title.setValue(draft.title);
          this.editor.controls.description.setValue('');
          this.editor.controls.expectedExperienceYears.setValue(draft.expectedExperienceYears);
          this.questions.clear();
          for (const question of draft.questions) {
            this.questions.push(
              this.fb.nonNullable.group({
                type: [question.type],
                stem: [question.stem, Validators.required],
                scoringMode: [question.scoringMode],
                creditMode: new FormControl<CreditMode | null>(question.creditMode),
                points: [question.points, [Validators.required, Validators.min(1)]],
                body: new FormControl<QuestionBody>(question.body, { nonNullable: true }),
              }),
            );
          }
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.showError(error);
        },
      });
  }

  save(): void {
    const current = this.draft();
    if (!current || this.editor.invalid) {
      this.editor.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const value = this.editor.getRawValue();
    this.draftsApi
      .accept(current.id, {
        title: value.title.trim(),
        description: value.description.trim(),
        expectedExperienceYears: value.expectedExperienceYears,
        questions: value.questions.map((question) => ({
          type: question['type'] as QuestionType,
          stem: String(question['stem'] ?? ''),
          scoringMode: question['scoringMode'] as ScoringMode,
          creditMode: (question['creditMode'] as CreditMode | null) ?? undefined,
          points: Number(question['points']),
          body: question['body'] as QuestionBody,
        })),
      })
      .subscribe({
        next: (accepted) => {
          this.savedQuizId.set(accepted.quizId);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.showError(error);
        },
      });
  }

  private showError(error: unknown): void {
    const status = PageStatus.fromError(error);
    this.error.set(status.message);
    this.correlationId.set(status.correlationId);
  }
}
