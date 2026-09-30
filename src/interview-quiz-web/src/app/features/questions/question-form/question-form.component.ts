import { NgTemplateOutlet } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BankQuestionResponse, CreditMode, QuestionType, ScoringMode } from '../../../core/api/contracts';
import { QuestionsApi } from '../../../core/api/questions-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import {
  BankItemDraft,
  BankQuestionDraft,
  bankQuestionToDraft,
  ChoiceOptionDraft,
  CREDIT_MODE_LABELS,
  CREDIT_MODES,
  defaultBankQuestionDraft,
  defaultBodyForType,
  defaultCreditMode,
  defaultScoringMode,
  EXPERIENCE_MAX,
  EXPERIENCE_MIN,
  FieldError,
  LongTextBodyDraft,
  newItemId,
  OrderingItemDraft,
  PerSlotDraft,
  POINTS_MAX,
  POINTS_MIN,
  QUESTION_STEM_MAX,
  QUESTION_TYPE_LABELS,
  QUESTION_TYPES,
  QuestionBodyDraft,
  QUIZ_TITLE_MAX,
  requiresCreditMode,
  SCORING_MODE_LABELS,
  SCORING_MODES,
  SharedBankSlotDraft,
  ShortAnswerDraft,
  toCreateBankQuestionRequest,
  toUpdateBankQuestionRequest,
  validateBankQuestionDraft,
} from '../../quizzes/quiz-form.mapper';

@Component({
  selector: 'app-question-form',
  imports: [NgTemplateOutlet, ReactiveFormsModule, RouterLink, HasPermission, PageStatus],
  templateUrl: './question-form.component.html',
  styleUrl: './question-form.component.css',
})
export class QuestionForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(QuestionsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly permissions = inject(PermissionService);

  readonly codes = PermissionCodes;
  readonly types = QUESTION_TYPES;
  readonly typeLabels = QUESTION_TYPE_LABELS;
  readonly scoringModes = SCORING_MODES;
  readonly scoringLabels = SCORING_MODE_LABELS;
  readonly creditModes = CREDIT_MODES;
  readonly creditLabels = CREDIT_MODE_LABELS;
  readonly titleMax = QUIZ_TITLE_MAX;
  readonly stemMax = QUESTION_STEM_MAX;
  readonly pointsMin = POINTS_MIN;
  readonly pointsMax = POINTS_MAX;
  readonly experienceMin = EXPERIENCE_MIN;
  readonly experienceMax = EXPERIENCE_MAX;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly archiving = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly isNew = signal(true);
  readonly archivedAtUtc = signal<string | null>(null);
  readonly fieldErrors = signal<ReadonlyMap<string, string>>(new Map());
  readonly canWrite = this.permissions.hasPermission(PermissionCodes.QuestionsWrite);

  private questionId: string | null = null;
  private rowVersion = 0;

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(QUIZ_TITLE_MAX)]],
    expectedExperienceYears: [
      0,
      [Validators.required, Validators.min(EXPERIENCE_MIN), Validators.max(EXPERIENCE_MAX)],
    ],
    tags: this.fb.array<ReturnType<QuestionForm['createTagGroup']>>([]),
    type: this.fb.nonNullable.control<QuestionType>('multipleChoiceSingle', Validators.required),
    stem: ['', [Validators.required, Validators.maxLength(QUESTION_STEM_MAX)]],
    points: [1, [Validators.required, Validators.min(POINTS_MIN), Validators.max(POINTS_MAX)]],
    scoringMode: this.fb.nonNullable.control<ScoringMode>('auto', Validators.required),
    creditMode: new FormControl<CreditMode | null>(null),
    body: this.createBodyGroup('multipleChoiceSingle', defaultBodyForType('multipleChoiceSingle')),
  });

  get tags(): FormArray {
    return this.form.controls.tags;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || id === 'new') {
      this.isNew.set(true);
      this.patchDraft(defaultBankQuestionDraft());
      this.loading.set(false);
      if (!this.canWrite) {
        this.error.set('You do not have permission to create bank questions.');
        this.form.disable();
      }
      return;
    }

    this.isNew.set(false);
    this.questionId = id;
    this.api.get(id).subscribe({
      next: (question) => {
        this.patch(question);
        this.loading.set(false);
        if (!this.canWrite) {
          this.form.disable();
        }
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.loading.set(false);
      },
    });
  }

  tagGroup(index: number): FormGroup {
    return this.tags.at(index) as FormGroup;
  }

  questionType(): QuestionType {
    return this.form.controls.type.value;
  }

  bodyGroup(): FormGroup {
    return this.form.controls.body as FormGroup;
  }

  optionsArray(): FormArray {
    return this.bodyGroup().get('options') as FormArray;
  }

  optionGroup(optionIndex: number): FormGroup {
    return this.optionsArray().at(optionIndex) as FormGroup;
  }

  answersArray(): FormArray {
    return this.bodyGroup().get('acceptableAnswers') as FormArray;
  }

  answerGroup(answerIndex: number): FormGroup {
    return this.answersArray().at(answerIndex) as FormGroup;
  }

  bankArray(): FormArray {
    return this.bodyGroup().get('bank') as FormArray;
  }

  bankGroup(bankIndex: number): FormGroup {
    return this.bankArray().at(bankIndex) as FormGroup;
  }

  slotsArray(): FormArray {
    return this.bodyGroup().get('slots') as FormArray;
  }

  slotGroup(slotIndex: number): FormGroup {
    return this.slotsArray().at(slotIndex) as FormGroup;
  }

  slotOptions(slotIndex: number): FormArray {
    return this.slotGroup(slotIndex).get('options') as FormArray;
  }

  slotOptionGroup(slotIndex: number, optionIndex: number): FormGroup {
    return this.slotOptions(slotIndex).at(optionIndex) as FormGroup;
  }

  itemsArray(): FormArray {
    return this.bodyGroup().get('items') as FormArray;
  }

  itemGroup(itemIndex: number): FormGroup {
    return this.itemsArray().at(itemIndex) as FormGroup;
  }

  bankItems(): BankItemDraft[] {
    return (this.bankArray().getRawValue() as BankItemDraft[]) ?? [];
  }

  needsCreditMode(): boolean {
    return requiresCreditMode(this.questionType());
  }

  scoringDisabled(mode: ScoringMode): boolean {
    return mode === 'auto' && this.questionType() === 'longText';
  }

  errorFor(path: string): string | null {
    return this.fieldErrors().get(path) ?? null;
  }

  addTag(key = '', value = ''): void {
    this.tags.push(this.createTagGroup(key, value));
  }

  removeTag(index: number): void {
    this.tags.removeAt(index);
  }

  onTypeChange(): void {
    const type = this.questionType();
    this.form.setControl('body', this.createBodyGroup(type, defaultBodyForType(type)));
    this.form.patchValue({
      scoringMode: defaultScoringMode(type),
      creditMode: defaultCreditMode(type),
    });
  }

  addOption(): void {
    this.optionsArray().push(this.createOptionGroup({ id: newItemId('opt'), text: '', isCorrect: false }));
  }

  removeOption(optionIndex: number): void {
    const options = this.optionsArray();
    if (options.length <= 2) {
      return;
    }
    options.removeAt(optionIndex);
  }

  markCorrect(optionIndex: number, event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement) || !target.checked) {
      return;
    }
    if (this.questionType() !== 'multipleChoiceSingle') {
      return;
    }
    this.optionsArray().controls.forEach((control, index) => {
      control.patchValue({ isCorrect: index === optionIndex });
    });
  }

  addAnswer(): void {
    this.answersArray().push(this.createAnswerGroup({ value: '' }));
  }

  removeAnswer(answerIndex: number): void {
    this.answersArray().removeAt(answerIndex);
  }

  addBankItem(): void {
    this.bankArray().push(this.createBankGroup({ id: newItemId('item'), text: '', isDistractor: false }));
  }

  removeBankItem(bankIndex: number): void {
    const bank = this.bankArray();
    if (bank.length <= 1) {
      return;
    }
    bank.removeAt(bankIndex);
  }

  addSharedSlot(): void {
    const firstBank = this.bankItems()[0];
    this.slotsArray().push(
      this.createSharedSlotGroup({
        id: newItemId('slot'),
        label: '',
        correctItemId: firstBank?.id ?? '',
      }),
    );
  }

  removeSharedSlot(slotIndex: number): void {
    const slots = this.slotsArray();
    if (slots.length <= 1) {
      return;
    }
    slots.removeAt(slotIndex);
  }

  addPerSlot(): void {
    this.slotsArray().push(
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

  removePerSlot(slotIndex: number): void {
    const slots = this.slotsArray();
    if (slots.length <= 1) {
      return;
    }
    slots.removeAt(slotIndex);
  }

  addPerSlotOption(slotIndex: number): void {
    this.slotOptions(slotIndex).push(
      this.createOptionGroup({ id: newItemId('opt'), text: '', isCorrect: false }),
    );
  }

  removePerSlotOption(slotIndex: number, optionIndex: number): void {
    const options = this.slotOptions(slotIndex);
    if (options.length <= 2) {
      return;
    }
    options.removeAt(optionIndex);
  }

  markSlotCorrect(slotIndex: number, optionIndex: number, event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement) || !target.checked) {
      return;
    }
    this.slotOptions(slotIndex).controls.forEach((control, index) => {
      control.patchValue({ isCorrect: index === optionIndex });
    });
  }

  addOrderingItem(): void {
    const items = this.itemsArray();
    items.push(
      this.createOrderingItemGroup({
        id: newItemId('item'),
        text: '',
        correctIndex: items.length,
      }),
    );
  }

  removeOrderingItem(itemIndex: number): void {
    const items = this.itemsArray();
    if (items.length <= 2) {
      return;
    }
    items.removeAt(itemIndex);
    [...items.controls]
      .sort(
        (left, right) =>
          Number(left.get('correctIndex')?.value) - Number(right.get('correctIndex')?.value),
      )
      .forEach((control, index) => control.patchValue({ correctIndex: index }));
  }

  submit(): void {
    this.error.set(null);
    this.fieldErrors.set(new Map());
    if (!this.canWrite) {
      return;
    }

    const draft = this.toDraft();
    const errors = validateBankQuestionDraft(draft);
    if (this.form.invalid || errors.length > 0) {
      this.form.markAllAsTouched();
      this.fieldErrors.set(this.toErrorMap(errors));
      this.focusFirstInvalid(errors);
      return;
    }

    this.saving.set(true);
    const request$ =
      this.isNew() || !this.questionId
        ? this.api.create(toCreateBankQuestionRequest(draft))
        : this.api.update(this.questionId, toUpdateBankQuestionRequest(draft, this.rowVersion));

    request$.subscribe({
      next: (question) => {
        if (this.isNew()) {
          void this.router.navigate(['/questions', question.id]);
          return;
        }
        this.patch(question);
        this.saving.set(false);
      },
      error: (err: unknown) => {
        this.fail(err);
        this.saving.set(false);
      },
    });
  }

  archive(): void {
    if (!this.canWrite || this.isNew() || !this.questionId || this.archivedAtUtc()) {
      return;
    }
    this.error.set(null);
    this.archiving.set(true);
    this.api.archive(this.questionId).subscribe({
      next: (question) => {
        this.patch(question);
        this.archiving.set(false);
      },
      error: (err: unknown) => {
        this.fail(err);
        this.archiving.set(false);
      },
    });
  }

  unarchive(): void {
    if (!this.canWrite || this.isNew() || !this.questionId || !this.archivedAtUtc()) {
      return;
    }
    this.error.set(null);
    this.archiving.set(true);
    this.api.unarchive(this.questionId).subscribe({
      next: (question) => {
        this.patch(question);
        this.archiving.set(false);
      },
      error: (err: unknown) => {
        this.fail(err);
        this.archiving.set(false);
      },
    });
  }

  private fail(err: unknown): void {
    const mapped = PageStatus.fromError(err);
    this.error.set(mapped.message);
    this.correlationId.set(mapped.correlationId);
  }

  private toDraft(): BankQuestionDraft {
    const raw = this.form.getRawValue();
    return {
      title: raw.title,
      tags: raw.tags,
      expectedExperienceYears: raw.expectedExperienceYears,
      type: raw.type,
      stem: raw.stem,
      points: raw.points,
      scoringMode: raw.scoringMode,
      creditMode: raw.creditMode,
      body: raw.body as QuestionBodyDraft,
    };
  }

  private toErrorMap(errors: FieldError[]): Map<string, string> {
    const map = new Map<string, string>();
    for (const error of errors) {
      if (!map.has(error.path)) {
        map.set(error.path, error.message);
      }
    }
    return map;
  }

  private focusFirstInvalid(errors: FieldError[]): void {
    const path = errors[0]?.path;
    const selector = path ? `#question-form [data-field="${path}"]` : '#question-form .ng-invalid';
    document.querySelector<HTMLElement>(selector)?.focus();
  }

  private patch(question: BankQuestionResponse): void {
    this.rowVersion = question.rowVersion;
    this.questionId = question.id;
    this.archivedAtUtc.set(question.archivedAtUtc);
    this.patchDraft(bankQuestionToDraft(question));
  }

  private patchDraft(draft: BankQuestionDraft): void {
    this.form.patchValue({
      title: draft.title,
      expectedExperienceYears: draft.expectedExperienceYears,
      type: draft.type,
      stem: draft.stem,
      points: draft.points,
      scoringMode: draft.scoringMode,
      creditMode: draft.creditMode,
    });
    this.tags.clear();
    for (const tag of draft.tags) {
      this.addTag(tag.key, tag.value);
    }
    this.form.setControl('body', this.createBodyGroup(draft.type, draft.body));
  }

  private createTagGroup(key: string, value: string) {
    return this.fb.nonNullable.group({
      key: [key, Validators.maxLength(64)],
      value: [value, Validators.maxLength(256)],
    });
  }

  private createBodyGroup(type: QuestionType, body: QuestionBodyDraft): FormGroup {
    switch (type) {
      case 'multipleChoiceSingle':
      case 'multipleChoiceMulti': {
        const options = 'options' in body ? body.options : [];
        return this.fb.nonNullable.group({
          options: this.fb.array(options.map((option) => this.createOptionGroup(option))),
        });
      }
      case 'trueFalse':
        return this.fb.nonNullable.group({
          correct: ['correct' in body ? body.correct : true],
        });
      case 'shortText': {
        const answers = 'acceptableAnswers' in body ? body.acceptableAnswers : [];
        return this.fb.nonNullable.group({
          acceptableAnswers: this.fb.array(answers.map((answer) => this.createAnswerGroup(answer))),
          caseSensitive: ['caseSensitive' in body ? body.caseSensitive : false],
        });
      }
      case 'longText': {
        const longBody = body as LongTextBodyDraft;
        return this.fb.group({
          maxLength: [longBody.maxLength],
          guidance: [longBody.guidance ?? '', Validators.maxLength(QUESTION_STEM_MAX)],
        });
      }
      case 'dragDropSharedBank': {
        const slots =
          'slots' in body && Array.isArray((body as { slots: SharedBankSlotDraft[] }).slots)
            ? (body as { slots: SharedBankSlotDraft[] }).slots
            : [];
        const bank = 'bank' in body ? body.bank : [];
        return this.fb.nonNullable.group({
          slots: this.fb.array(slots.map((slot) => this.createSharedSlotGroup(slot))),
          bank: this.fb.array(bank.map((item) => this.createBankGroup(item))),
        });
      }
      case 'dragDropPerSlot': {
        const slots = 'slots' in body ? (body as { slots: PerSlotDraft[] }).slots : [];
        return this.fb.nonNullable.group({
          slots: this.fb.array(slots.map((slot) => this.createPerSlotGroup(slot))),
        });
      }
      case 'ordering': {
        const items = 'items' in body ? body.items : [];
        return this.fb.nonNullable.group({
          items: this.fb.array(items.map((item) => this.createOrderingItemGroup(item))),
        });
      }
    }
  }

  private createOptionGroup(option: ChoiceOptionDraft) {
    return this.fb.nonNullable.group({
      id: [option.id],
      text: [option.text, Validators.required],
      isCorrect: [option.isCorrect],
    });
  }

  private createAnswerGroup(answer: ShortAnswerDraft) {
    return this.fb.nonNullable.group({
      value: [answer.value],
    });
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

  private createOrderingItemGroup(item: OrderingItemDraft) {
    return this.fb.nonNullable.group({
      id: [item.id],
      text: [item.text, Validators.required],
      correctIndex: [item.correctIndex, Validators.required],
    });
  }
}
