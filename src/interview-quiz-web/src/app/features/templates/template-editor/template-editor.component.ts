import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TemplateResponse, TemplateVersionResponse } from '../../../core/api/contracts';
import { TemplatesApi } from '../../../core/api/templates-api.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import {
  BankItemDraft,
  CREDIT_MODE_LABELS,
  CREDIT_MODES,
  DragDropPerSlotBodyDraft,
  DragDropSharedBankBodyDraft,
  PerSlotDraft,
  QUESTION_TYPE_LABELS,
  type QuestionDraft,
  QuestionBodyDraft,
  questionToDraft,
  requiresCreditMode,
  SCORING_MODE_LABELS,
  SCORING_MODES,
  SharedBankSlotDraft,
  toQuestionRequest,
  newItemId,
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
  readonly typeLabels = QUESTION_TYPE_LABELS;
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
    questions: this.fb.array<FormGroup>([]),
  });

  private templateId: string | null = null;
  private drafts: QuestionDraft[] = [];

  get questions(): FormArray<FormGroup> {
    return this.form.controls.questions;
  }

  draftsType(index: number): QuestionDraft['type'] {
    return this.drafts[index]?.type ?? 'shortText';
  }

  questionGroup(index: number): FormGroup {
    return this.questions.at(index);
  }

  bodyGroup(index: number): FormGroup | null {
    return this.questionGroup(index).get('body') as FormGroup | null;
  }

  bankArray(index: number): FormArray {
    return this.bodyGroup(index)?.get('bank') as FormArray;
  }

  bankGroup(questionIndex: number, bankIndex: number): FormGroup {
    return this.bankArray(questionIndex).at(bankIndex) as FormGroup;
  }

  slotsArray(index: number): FormArray {
    return this.bodyGroup(index)?.get('slots') as FormArray;
  }

  slotGroup(questionIndex: number, slotIndex: number): FormGroup {
    return this.slotsArray(questionIndex).at(slotIndex) as FormGroup;
  }

  slotOptions(questionIndex: number, slotIndex: number): FormArray {
    return this.slotGroup(questionIndex, slotIndex).get('options') as FormArray;
  }

  slotOptionGroup(questionIndex: number, slotIndex: number, optionIndex: number): FormGroup {
    return this.slotOptions(questionIndex, slotIndex).at(optionIndex) as FormGroup;
  }

  bankItems(index: number): BankItemDraft[] {
    return (this.bankArray(index)?.getRawValue() as BankItemDraft[]) ?? [];
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

  addBankItem(questionIndex: number): void {
    this.bankArray(questionIndex).push(
      this.createBankGroup({ id: newItemId('item'), text: '', isDistractor: false }),
    );
  }

  removeBankItem(questionIndex: number, bankIndex: number): void {
    const bank = this.bankArray(questionIndex);
    if (bank.length <= 1) {
      return;
    }
    bank.removeAt(bankIndex);
  }

  addSharedSlot(questionIndex: number): void {
    const firstBank = this.bankItems(questionIndex)[0];
    this.slotsArray(questionIndex).push(
      this.createSharedSlotGroup({
        id: newItemId('slot'),
        label: '',
        correctItemId: firstBank?.id ?? '',
      }),
    );
  }

  removeSharedSlot(questionIndex: number, slotIndex: number): void {
    const slots = this.slotsArray(questionIndex);
    if (slots.length <= 1) {
      return;
    }
    slots.removeAt(slotIndex);
  }

  addPerSlot(questionIndex: number): void {
    this.slotsArray(questionIndex).push(
      this.createPerSlotGroup({
        id: newItemId('slot'),
        label: '',
        options: [
          { id: newItemId('opt'), text: '', isCorrect: true },
          { id: newItemId('opt'), text: '', isCorrect: false },
        ],
      }),
    );
  }

  removePerSlot(questionIndex: number, slotIndex: number): void {
    const slots = this.slotsArray(questionIndex);
    if (slots.length <= 1) {
      return;
    }
    slots.removeAt(slotIndex);
  }

  addPerSlotOption(questionIndex: number, slotIndex: number): void {
    this.slotOptions(questionIndex, slotIndex).push(
      this.createOptionGroup({ id: newItemId('opt'), text: '', isCorrect: false }),
    );
  }

  removePerSlotOption(questionIndex: number, slotIndex: number, optionIndex: number): void {
    const options = this.slotOptions(questionIndex, slotIndex);
    if (options.length <= 2) {
      return;
    }
    options.removeAt(optionIndex);
  }

  markSlotCorrect(questionIndex: number, slotIndex: number, optionIndex: number, event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement) || !target.checked) {
      return;
    }
    this.slotOptions(questionIndex, slotIndex).controls.forEach((control, index) => {
      control.patchValue({ isCorrect: index === optionIndex });
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
        body: this.bodyFromForm(index, draft),
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
      const group = this.fb.nonNullable.group({
        stem: [draft.stem, [Validators.required, Validators.maxLength(8000)]],
        points: [draft.points, [Validators.required, Validators.min(1), Validators.max(10000)]],
        scoringMode: [draft.scoringMode, Validators.required],
        creditMode: [draft.creditMode],
      });
      const body = this.createBodyGroup(draft);
      if (body) {
        (group as FormGroup).addControl('body', body);
      }
      this.questions.push(group);
    }
  }

  private bodyFromForm(index: number, draft: QuestionDraft): QuestionBodyDraft {
    if (draft.type === 'dragDropSharedBank') {
      return this.bodyGroup(index)?.getRawValue() as DragDropSharedBankBodyDraft;
    }
    if (draft.type === 'dragDropPerSlot') {
      return this.bodyGroup(index)?.getRawValue() as DragDropPerSlotBodyDraft;
    }
    return draft.body;
  }

  private createBodyGroup(draft: QuestionDraft): FormGroup | null {
    if (draft.type === 'dragDropSharedBank') {
      const body = draft.body as DragDropSharedBankBodyDraft;
      return this.fb.nonNullable.group({
        slots: this.fb.array(body.slots.map((slot) => this.createSharedSlotGroup(slot))),
        bank: this.fb.array(body.bank.map((item) => this.createBankGroup(item))),
      });
    }
    if (draft.type === 'dragDropPerSlot') {
      const body = draft.body as DragDropPerSlotBodyDraft;
      return this.fb.nonNullable.group({
        slots: this.fb.array(body.slots.map((slot) => this.createPerSlotGroup(slot))),
      });
    }
    return null;
  }

  private createBankGroup(item: BankItemDraft) {
    return this.fb.nonNullable.group({
      id: [item.id],
      text: [item.text, Validators.required],
      isDistractor: [item.isDistractor],
    });
  }

  private createSharedSlotGroup(slot: SharedBankSlotDraft) {
    return this.fb.nonNullable.group({
      id: [slot.id],
      label: [slot.label, Validators.required],
      correctItemId: [slot.correctItemId, Validators.required],
    });
  }

  private createPerSlotGroup(slot: PerSlotDraft) {
    return this.fb.nonNullable.group({
      id: [slot.id],
      label: [slot.label, Validators.required],
      options: this.fb.array(slot.options.map((option) => this.createOptionGroup(option))),
    });
  }

  private createOptionGroup(option: { id: string; text: string; isCorrect: boolean }) {
    return this.fb.nonNullable.group({
      id: [option.id],
      text: [option.text, Validators.required],
      isCorrect: [option.isCorrect],
    });
  }

  private fail(err: unknown): void {
    const mapped = PageStatus.fromError(err);
    this.error.set(mapped.message);
    this.correlationId.set(mapped.correlationId);
    this.loading.set(false);
  }
}
