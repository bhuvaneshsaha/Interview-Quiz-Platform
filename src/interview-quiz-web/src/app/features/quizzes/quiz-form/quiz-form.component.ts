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
import { CreditMode, OpeningResponse, QuestionType, QuizResponse, ScoringMode } from '../../../core/api/contracts';
import { OpeningsApi } from '../../../core/api/openings-api.service';
import { QuizzesApi } from '../../../core/api/quizzes-api.service';
import { HasPermission } from '../../../core/permissions/has-permission.directive';
import { PermissionCodes } from '../../../core/permissions/permission-codes';
import { PermissionService } from '../../../core/permissions/permission.service';
import { PageStatus } from '../../../shared/page-status/page-status.component';
import {
  BankItemDraft,
  ChoiceOptionDraft,
  CREDIT_MODE_LABELS,
  CREDIT_MODES,
  defaultBodyForType,
  defaultCreditMode,
  defaultQuestionDraft,
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
  QuestionDraft,
  QUIZ_DESCRIPTION_MAX,
  QUIZ_TITLE_MAX,
  QuizDraft,
  requiresCreditMode,
  SCORING_MODE_LABELS,
  SCORING_MODES,
  SharedBankSlotDraft,
  ShortAnswerDraft,
  toCreateRequest,
  toUpdateRequest,
  validateQuizDraft,
  quizToDraft,
} from '../quiz-form.mapper';

@Component({
  selector: 'app-quiz-form',
  imports: [NgTemplateOutlet, ReactiveFormsModule, RouterLink, HasPermission, PageStatus],
  templateUrl: './quiz-form.component.html',
  styleUrl: './quiz-form.component.css',
})
export class QuizForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(QuizzesApi);
  private readonly openingsApi = inject(OpeningsApi);
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
  readonly descriptionMax = QUIZ_DESCRIPTION_MAX;
  readonly stemMax = QUESTION_STEM_MAX;
  readonly pointsMin = POINTS_MIN;
  readonly pointsMax = POINTS_MAX;
  readonly experienceMin = EXPERIENCE_MIN;
  readonly experienceMax = EXPERIENCE_MAX;

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly correlationId = signal<string | null>(null);
  readonly isNew = signal(true);
  readonly fieldErrors = signal<ReadonlyMap<string, string>>(new Map());
  readonly openings = signal<OpeningResponse[]>([]);
  readonly canWrite = this.permissions.hasPermission(PermissionCodes.QuizzesWrite);
  readonly canReadOpenings = this.permissions.hasPermission(PermissionCodes.OpeningsRead);

  readonly addType = new FormControl<QuestionType>('multipleChoiceSingle', { nonNullable: true });

  private quizId: string | null = null;
  private rowVersion = 0;

  readonly form = this.fb.nonNullable.group({
    openingId: ['', Validators.required],
    title: ['', [Validators.required, Validators.maxLength(QUIZ_TITLE_MAX)]],
    description: ['', Validators.maxLength(QUIZ_DESCRIPTION_MAX)],
    expectedExperienceYears: [
      0,
      [Validators.required, Validators.min(EXPERIENCE_MIN), Validators.max(EXPERIENCE_MAX)],
    ],
    tags: this.fb.array<ReturnType<QuizForm['createTagGroup']>>([]),
    questions: this.fb.array<ReturnType<QuizForm['createQuestionGroup']>>([]),
  });

  get tags(): FormArray {
    return this.form.controls.tags;
  }

  get questions(): FormArray {
    return this.form.controls.questions;
  }

  ngOnInit(): void {
    if (this.canReadOpenings) {
      this.openingsApi.list(1, 100).subscribe({
        next: (result) => this.openings.set(result.items),
        error: () => this.openings.set([]),
      });
    }

    const id = this.route.snapshot.paramMap.get('id');
    if (!id || id === 'new') {
      this.isNew.set(true);
      this.loading.set(false);
      if (!this.canWrite) {
        this.error.set('You do not have permission to create quizzes.');
        this.form.disable();
      }
      return;
    }

    this.isNew.set(false);
    this.quizId = id;
    this.api.get(id).subscribe({
      next: (quiz) => {
        this.patch(quiz);
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

  questionGroup(index: number): FormGroup {
    return this.questions.at(index) as FormGroup;
  }

  questionType(index: number): QuestionType {
    return this.questionGroup(index).controls['type'].value as QuestionType;
  }

  bodyGroup(index: number): FormGroup {
    return this.questionGroup(index).get('body') as FormGroup;
  }

  optionsArray(index: number): FormArray {
    return this.bodyGroup(index).get('options') as FormArray;
  }

  optionGroup(questionIndex: number, optionIndex: number): FormGroup {
    return this.optionsArray(questionIndex).at(optionIndex) as FormGroup;
  }

  answersArray(index: number): FormArray {
    return this.bodyGroup(index).get('acceptableAnswers') as FormArray;
  }

  answerGroup(questionIndex: number, answerIndex: number): FormGroup {
    return this.answersArray(questionIndex).at(answerIndex) as FormGroup;
  }

  bankArray(index: number): FormArray {
    return this.bodyGroup(index).get('bank') as FormArray;
  }

  bankGroup(questionIndex: number, bankIndex: number): FormGroup {
    return this.bankArray(questionIndex).at(bankIndex) as FormGroup;
  }

  slotsArray(index: number): FormArray {
    return this.bodyGroup(index).get('slots') as FormArray;
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

  itemsArray(index: number): FormArray {
    return this.bodyGroup(index).get('items') as FormArray;
  }

  itemGroup(questionIndex: number, itemIndex: number): FormGroup {
    return this.itemsArray(questionIndex).at(itemIndex) as FormGroup;
  }

  bankItems(index: number): BankItemDraft[] {
    return (this.bankArray(index).getRawValue() as BankItemDraft[]) ?? [];
  }

  needsCreditMode(index: number): boolean {
    return requiresCreditMode(this.questionType(index));
  }

  scoringDisabled(mode: ScoringMode, index: number): boolean {
    return mode === 'auto' && this.questionType(index) === 'longText';
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

  addQuestion(): void {
    this.questions.push(this.createQuestionGroup(defaultQuestionDraft(this.addType.value)));
  }

  removeQuestion(index: number): void {
    this.questions.removeAt(index);
  }

  moveQuestion(index: number, delta: number): void {
    const target = index + delta;
    if (target < 0 || target >= this.questions.length) {
      return;
    }
    const current = this.questions.at(index);
    this.questions.removeAt(index);
    this.questions.insert(target, current);
  }

  onTypeChange(index: number): void {
    const group = this.questionGroup(index);
    const type = group.controls['type'].value as QuestionType;
    group.setControl('body', this.createBodyGroup(type, defaultBodyForType(type)));
    group.patchValue({
      scoringMode: defaultScoringMode(type),
      creditMode: defaultCreditMode(type),
    });
  }

  addOption(questionIndex: number): void {
    this.optionsArray(questionIndex).push(
      this.createOptionGroup({ id: newItemId('opt'), text: '', isCorrect: false }),
    );
  }

  removeOption(questionIndex: number, optionIndex: number): void {
    const options = this.optionsArray(questionIndex);
    if (options.length <= 2) {
      return;
    }
    options.removeAt(optionIndex);
  }

  markCorrect(questionIndex: number, optionIndex: number, event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }
    if (this.questionType(questionIndex) !== 'multipleChoiceSingle') {
      return;
    }
    if (!target.checked) {
      return;
    }
    this.optionsArray(questionIndex).controls.forEach((control, index) => {
      control.patchValue({ isCorrect: index === optionIndex });
    });
  }

  addAnswer(questionIndex: number): void {
    this.answersArray(questionIndex).push(this.createAnswerGroup({ value: '' }));
  }

  removeAnswer(questionIndex: number, answerIndex: number): void {
    this.answersArray(questionIndex).removeAt(answerIndex);
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

  addOrderingItem(questionIndex: number): void {
    const items = this.itemsArray(questionIndex);
    items.push(
      this.createOrderingItemGroup({
        id: newItemId('item'),
        text: '',
        correctIndex: items.length,
      }),
    );
  }

  removeOrderingItem(questionIndex: number, itemIndex: number): void {
    const items = this.itemsArray(questionIndex);
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

    const draft = this.form.getRawValue() as QuizDraft;
    const errors = validateQuizDraft(draft);
    if (this.form.invalid || errors.length > 0) {
      this.form.markAllAsTouched();
      this.fieldErrors.set(this.toErrorMap(errors));
      this.focusFirstInvalid(errors);
      return;
    }

    this.saving.set(true);
    const request$ =
      this.isNew() || !this.quizId
        ? this.api.create(toCreateRequest(draft))
        : this.api.update(this.quizId, toUpdateRequest(draft, this.quizId, this.rowVersion));

    request$.subscribe({
      next: (quiz) => {
        if (this.isNew()) {
          void this.router.navigate(['/quizzes', quiz.id]);
          return;
        }
        this.patch(quiz);
        this.saving.set(false);
      },
      error: (err: unknown) => {
        const mapped = PageStatus.fromError(err);
        this.error.set(mapped.message);
        this.correlationId.set(mapped.correlationId);
        this.saving.set(false);
      },
    });
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
    const selector = path
      ? `#quiz-form [data-field="${path}"]`
      : '#quiz-form .ng-invalid';
    document.querySelector<HTMLElement>(selector)?.focus();
  }

  private patch(quiz: QuizResponse): void {
    this.rowVersion = quiz.rowVersion;
    this.quizId = quiz.id;
    const draft = quizToDraft(quiz);
    this.form.patchValue({
      openingId: draft.openingId,
      title: draft.title,
      description: draft.description,
      expectedExperienceYears: draft.expectedExperienceYears,
    });
    this.tags.clear();
    for (const tag of draft.tags) {
      this.addTag(tag.key, tag.value);
    }
    this.questions.clear();
    for (const question of draft.questions) {
      this.questions.push(this.createQuestionGroup(question));
    }
  }

  private createTagGroup(key: string, value: string) {
    return this.fb.nonNullable.group({
      key: [key, Validators.maxLength(64)],
      value: [value, Validators.maxLength(256)],
    });
  }

  private createQuestionGroup(question: QuestionDraft) {
    return this.fb.nonNullable.group({
      id: [question.id],
      type: [question.type, Validators.required],
      stem: [question.stem, [Validators.required, Validators.maxLength(QUESTION_STEM_MAX)]],
      points: [
        question.points,
        [Validators.required, Validators.min(POINTS_MIN), Validators.max(POINTS_MAX)],
      ],
      scoringMode: [question.scoringMode, Validators.required],
      creditMode: new FormControl<CreditMode | null>(question.creditMode),
      body: this.createBodyGroup(question.type, question.body),
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
        const slots = 'slots' in body && Array.isArray((body as { slots: SharedBankSlotDraft[] }).slots)
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
