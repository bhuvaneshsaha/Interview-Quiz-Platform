import {
  BankItemBody,
  BankQuestionResponse,
  ChoiceOptionBody,
  CreateBankQuestionRequest,
  CreateQuizRequest,
  CreditMode,
  DragDropPerSlotBody,
  DragDropSharedBankBody,
  LongTextBody,
  MultipleChoiceBody,
  OrderingBody,
  PerSlotBody,
  QuestionBody,
  QuestionRequest,
  QuestionResponse,
  QuestionType,
  QuizResponse,
  ScoringMode,
  SharedBankSlotBody,
  ShortTextBody,
  TrueFalseBody,
  UpdateBankQuestionRequest,
  UpdateQuizRequest,
} from '../../core/api/contracts';

export const QUESTION_TYPES: readonly QuestionType[] = [
  'multipleChoiceSingle',
  'multipleChoiceMulti',
  'trueFalse',
  'shortText',
  'longText',
  'dragDropSharedBank',
  'dragDropPerSlot',
  'ordering',
] as const;

export const QUESTION_TYPE_LABELS: Record<QuestionType, string> = {
  multipleChoiceSingle: 'Multiple choice (single)',
  multipleChoiceMulti: 'Multiple choice (multi)',
  trueFalse: 'True / false',
  shortText: 'Short text',
  longText: 'Long text',
  dragDropSharedBank: 'Drag and drop (shared bank)',
  dragDropPerSlot: 'Drag and drop (per slot)',
  ordering: 'Ordering',
};

export const SCORING_MODES: readonly ScoringMode[] = ['auto', 'aiAssist', 'humanOnly'];

export const SCORING_MODE_LABELS: Record<ScoringMode, string> = {
  auto: 'Auto',
  aiAssist: 'AI assist',
  humanOnly: 'Human only',
};

export const CREDIT_MODES: readonly CreditMode[] = ['partial', 'allOrNothing'];

export const CREDIT_MODE_LABELS: Record<CreditMode, string> = {
  partial: 'Partial',
  allOrNothing: 'All or nothing',
};

export const QUIZ_TITLE_MAX = 200;
export const QUIZ_DESCRIPTION_MAX = 8000;
export const QUESTION_STEM_MAX = 8000;
export const POINTS_MIN = 1;
export const POINTS_MAX = 10_000;
export const EXPERIENCE_MIN = 0;
export const EXPERIENCE_MAX = 80;
export const BODY_ID_MAX = 64;
export const TAG_KEY_MAX = 64;
export const TAG_VALUE_MAX = 256;

const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const TAG_KEY_PATTERN = /^[A-Za-z0-9._-]+$/;

export interface ChoiceOptionDraft {
  id: string;
  text: string;
  isCorrect: boolean;
}

export interface MultipleChoiceBodyDraft {
  options: ChoiceOptionDraft[];
}

export interface TrueFalseBodyDraft {
  correct: boolean;
}

export interface ShortAnswerDraft {
  value: string;
}

export interface ShortTextBodyDraft {
  acceptableAnswers: ShortAnswerDraft[];
  caseSensitive: boolean;
}

export interface LongTextBodyDraft {
  maxLength: number | null;
  guidance: string;
}

export interface SharedBankSlotDraft {
  id: string;
  label: string;
  correctItemId: string;
}

export interface BankItemDraft {
  id: string;
  text: string;
  isDistractor: boolean;
}

export interface DragDropSharedBankBodyDraft {
  slots: SharedBankSlotDraft[];
  bank: BankItemDraft[];
}

export interface PerSlotDraft {
  id: string;
  label: string;
  options: ChoiceOptionDraft[];
}

export interface DragDropPerSlotBodyDraft {
  slots: PerSlotDraft[];
}

export interface OrderingItemDraft {
  id: string;
  text: string;
  correctIndex: number;
}

export interface OrderingBodyDraft {
  items: OrderingItemDraft[];
}

export type QuestionBodyDraft =
  | MultipleChoiceBodyDraft
  | TrueFalseBodyDraft
  | ShortTextBodyDraft
  | LongTextBodyDraft
  | DragDropSharedBankBodyDraft
  | DragDropPerSlotBodyDraft
  | OrderingBodyDraft;

export interface QuestionDraft {
  id: string | null;
  type: QuestionType;
  stem: string;
  points: number;
  scoringMode: ScoringMode;
  creditMode: CreditMode | null;
  sourceQuestionId: string | null;
  body: QuestionBodyDraft;
}

export interface TagDraft {
  key: string;
  value: string;
}

export interface QuizDraft {
  openingId: string;
  title: string;
  description: string;
  expectedExperienceYears: number;
  tags: TagDraft[];
  questions: QuestionDraft[];
}

export interface BankQuestionDraft {
  title: string;
  tags: TagDraft[];
  expectedExperienceYears: number;
  type: QuestionType;
  stem: string;
  points: number;
  scoringMode: ScoringMode;
  creditMode: CreditMode | null;
  body: QuestionBodyDraft;
}

export interface FieldError {
  path: string;
  message: string;
}

export function isUuid(value: string): boolean {
  return UUID_PATTERN.test(value.trim());
}

function fieldPath(prefix: string, suffix: string): string {
  return prefix ? `${prefix}.${suffix}` : suffix;
}

export function requiresCreditMode(type: QuestionType): boolean {
  return type === 'multipleChoiceMulti' || type === 'ordering';
}

export function defaultScoringMode(type: QuestionType): ScoringMode {
  if (type === 'longText' || type === 'shortText') {
    return 'humanOnly';
  }
  return 'auto';
}

export function defaultCreditMode(type: QuestionType): CreditMode | null {
  return requiresCreditMode(type) ? 'partial' : null;
}

export function newItemId(prefix: string): string {
  const raw = crypto.randomUUID().replaceAll('-', '').slice(0, 12);
  return `${prefix}-${raw}`;
}

export function defaultBodyForType(type: QuestionType): QuestionBodyDraft {
  switch (type) {
    case 'multipleChoiceSingle':
    case 'multipleChoiceMulti':
      return {
        options: [
          { id: newItemId('opt'), text: '', isCorrect: true },
          { id: newItemId('opt'), text: '', isCorrect: false },
        ],
      };
    case 'trueFalse':
      return { correct: true };
    case 'shortText':
      return { acceptableAnswers: [], caseSensitive: false };
    case 'longText':
      return { maxLength: null, guidance: '' };
    case 'dragDropSharedBank': {
      const itemId = newItemId('item');
      return {
        slots: [{ id: newItemId('slot'), label: '', correctItemId: itemId }],
        bank: [{ id: itemId, text: '', isDistractor: false }],
      };
    }
    case 'dragDropPerSlot':
      return {
        slots: [
          {
            id: newItemId('slot'),
            label: '',
            options: [
              { id: newItemId('opt'), text: '', isCorrect: true },
              { id: newItemId('opt'), text: '', isCorrect: false },
            ],
          },
        ],
      };
    case 'ordering':
      return {
        items: [
          { id: newItemId('item'), text: '', correctIndex: 0 },
          { id: newItemId('item'), text: '', correctIndex: 1 },
        ],
      };
  }
}

export function defaultQuestionDraft(type: QuestionType): QuestionDraft {
  return {
    id: null,
    type,
    stem: '',
    points: 1,
    scoringMode: defaultScoringMode(type),
    creditMode: defaultCreditMode(type),
    sourceQuestionId: null,
    body: defaultBodyForType(type),
  };
}

export function defaultBankQuestionDraft(type: QuestionType = 'multipleChoiceSingle'): BankQuestionDraft {
  const question = defaultQuestionDraft(type);
  return {
    title: '',
    tags: [],
    expectedExperienceYears: 0,
    type: question.type,
    stem: question.stem,
    points: question.points,
    scoringMode: question.scoringMode,
    creditMode: question.creditMode,
    body: question.body,
  };
}

export function emptyQuizDraft(): QuizDraft {
  return {
    openingId: '',
    title: '',
    description: '',
    expectedExperienceYears: 0,
    tags: [],
    questions: [],
  };
}

export function quizToDraft(quiz: QuizResponse): QuizDraft {
  const questions = [...quiz.questions].sort((a, b) => a.sortOrder - b.sortOrder || a.id.localeCompare(b.id));
  return {
    openingId: quiz.openingId,
    title: quiz.title,
    description: quiz.description ?? '',
    expectedExperienceYears: quiz.expectedExperienceYears,
    tags: Object.entries(quiz.tags).map(([key, value]) => ({ key, value })),
    questions: questions.map(questionToDraft),
  };
}

export function toCreateRequest(draft: QuizDraft): CreateQuizRequest {
  const description = draft.description.trim();
  return {
    openingId: draft.openingId.trim(),
    title: draft.title.trim(),
    ...(description ? { description } : {}),
    expectedExperienceYears: Number(draft.expectedExperienceYears),
    tags: tagsFromDraft(draft.tags),
    questions: draft.questions.map((question, index) => toQuestionRequest(question, index)),
  };
}

export function toUpdateRequest(draft: QuizDraft, id: string, rowVersion: number): UpdateQuizRequest {
  return {
    ...toCreateRequest(draft),
    id,
    rowVersion,
  };
}

export function bankQuestionToDraft(question: BankQuestionResponse): BankQuestionDraft {
  return {
    title: question.title,
    tags: Object.entries(question.tags ?? {}).map(([key, value]) => ({ key, value })),
    expectedExperienceYears: question.expectedExperienceYears,
    type: question.type,
    stem: question.stem,
    points: question.points,
    scoringMode: question.scoringMode,
    creditMode: requiresCreditMode(question.type) ? question.creditMode : null,
    body: bodyToDraft(question.type, question.body),
  };
}

export function toCreateBankQuestionRequest(draft: BankQuestionDraft): CreateBankQuestionRequest {
  const request: CreateBankQuestionRequest = {
    title: draft.title.trim(),
    tags: tagsFromDraft(draft.tags),
    expectedExperienceYears: Number(draft.expectedExperienceYears),
    type: draft.type,
    stem: draft.stem.trim(),
    scoringMode: draft.scoringMode,
    points: Number(draft.points),
    body: toQuestionBody(draft.type, draft.body),
  };
  if (requiresCreditMode(draft.type) && draft.creditMode) {
    request.creditMode = draft.creditMode;
  }
  return request;
}

export function toUpdateBankQuestionRequest(
  draft: BankQuestionDraft,
  rowVersion: number,
): UpdateBankQuestionRequest {
  return {
    ...toCreateBankQuestionRequest(draft),
    rowVersion,
  };
}

export function validateQuizDraft(draft: QuizDraft): FieldError[] {
  const errors: FieldError[] = [];
  const openingId = draft.openingId.trim();
  if (!openingId) {
    errors.push({ path: 'openingId', message: 'Opening is required.' });
  } else if (!isUuid(openingId)) {
    errors.push({ path: 'openingId', message: 'Opening must be a valid id.' });
  }

  const title = draft.title.trim();
  if (!title) {
    errors.push({ path: 'title', message: 'Title is required.' });
  } else if (title.length > QUIZ_TITLE_MAX) {
    errors.push({ path: 'title', message: `Title cannot exceed ${QUIZ_TITLE_MAX} characters.` });
  }

  if (draft.description.trim().length > QUIZ_DESCRIPTION_MAX) {
    errors.push({
      path: 'description',
      message: `Description cannot exceed ${QUIZ_DESCRIPTION_MAX} characters.`,
    });
  }

  const years = Number(draft.expectedExperienceYears);
  if (!Number.isInteger(years) || years < EXPERIENCE_MIN || years > EXPERIENCE_MAX) {
    errors.push({
      path: 'expectedExperienceYears',
      message: `Expected experience must be between ${EXPERIENCE_MIN} and ${EXPERIENCE_MAX}.`,
    });
  }

  errors.push(...validateTags(draft.tags));

  draft.questions.forEach((question, index) => {
    errors.push(...validateQuestionDraft(question, `questions.${index}`));
  });

  return errors;
}

export function validateBankQuestionDraft(draft: BankQuestionDraft): FieldError[] {
  const errors: FieldError[] = [];
  const title = draft.title.trim();
  if (!title) {
    errors.push({ path: 'title', message: 'Title is required.' });
  } else if (title.length > QUIZ_TITLE_MAX) {
    errors.push({ path: 'title', message: `Title cannot exceed ${QUIZ_TITLE_MAX} characters.` });
  }

  const years = Number(draft.expectedExperienceYears);
  if (!Number.isInteger(years) || years < EXPERIENCE_MIN || years > EXPERIENCE_MAX) {
    errors.push({
      path: 'expectedExperienceYears',
      message: `Expected experience must be between ${EXPERIENCE_MIN} and ${EXPERIENCE_MAX}.`,
    });
  }

  errors.push(...validateTags(draft.tags));
  errors.push(
    ...validateQuestionDraft(
      {
        id: null,
        type: draft.type,
        stem: draft.stem,
        points: draft.points,
        scoringMode: draft.scoringMode,
        creditMode: draft.creditMode,
        sourceQuestionId: null,
        body: draft.body,
      },
      '',
    ),
  );
  return errors;
}

function validateTags(tags: TagDraft[]): FieldError[] {
  const errors: FieldError[] = [];
  const tagKeys = new Set<string>();
  tags.forEach((tag, index) => {
    const key = tag.key.trim();
    const value = tag.value.trim();
    if (!key && !value) {
      return;
    }
    if (!key) {
      errors.push({ path: `tags.${index}.key`, message: 'Tag key is required.' });
      return;
    }
    if (key.length > TAG_KEY_MAX) {
      errors.push({ path: `tags.${index}.key`, message: `Tag key cannot exceed ${TAG_KEY_MAX} characters.` });
    }
    if (!TAG_KEY_PATTERN.test(key)) {
      errors.push({
        path: `tags.${index}.key`,
        message: "Tag keys may contain letters, digits, '.', '_' and '-' only.",
      });
    }
    const keyLookup = key.toLowerCase();
    if (tagKeys.has(keyLookup)) {
      errors.push({ path: `tags.${index}.key`, message: `Duplicate tag key '${key}'.` });
    }
    tagKeys.add(keyLookup);
    if (!value) {
      errors.push({ path: `tags.${index}.value`, message: 'Tag value is required.' });
    } else if (value.length > TAG_VALUE_MAX) {
      errors.push({
        path: `tags.${index}.value`,
        message: `Tag value cannot exceed ${TAG_VALUE_MAX} characters.`,
      });
    }
  });
  return errors;
}

export function validateQuestionDraft(question: QuestionDraft, prefix: string): FieldError[] {
  const errors: FieldError[] = [];
  if (!QUESTION_TYPES.includes(question.type)) {
    errors.push({ path: fieldPath(prefix, 'type'), message: 'Unknown question type.' });
    return errors;
  }

  const stem = question.stem.trim();
  if (!stem) {
    errors.push({ path: fieldPath(prefix, 'stem'), message: 'Stem is required.' });
  } else if (stem.length > QUESTION_STEM_MAX) {
    errors.push({
      path: fieldPath(prefix, 'stem'),
      message: `Stem cannot exceed ${QUESTION_STEM_MAX} characters.`,
    });
  }

  const points = Number(question.points);
  if (!Number.isInteger(points) || points < POINTS_MIN || points > POINTS_MAX) {
    errors.push({
      path: fieldPath(prefix, 'points'),
      message: `Points must be between ${POINTS_MIN} and ${POINTS_MAX}.`,
    });
  }

  if (!SCORING_MODES.includes(question.scoringMode)) {
    errors.push({ path: fieldPath(prefix, 'scoringMode'), message: 'Unknown scoring mode.' });
  }

  if (question.type === 'longText' && question.scoringMode === 'auto') {
    errors.push({ path: fieldPath(prefix, 'scoringMode'), message: 'Long text questions cannot use auto scoring.' });
  }

  if (requiresCreditMode(question.type)) {
    if (question.creditMode !== 'partial' && question.creditMode !== 'allOrNothing') {
      errors.push({
        path: fieldPath(prefix, 'creditMode'),
        message: `Credit mode is required for ${QUESTION_TYPE_LABELS[question.type].toLowerCase()} questions.`,
      });
    }
  } else if (question.creditMode) {
    errors.push({
      path: fieldPath(prefix, 'creditMode'),
      message: `Credit mode must be omitted for ${question.type} questions.`,
    });
  }

  errors.push(...validateBody(question, prefix));
  return errors;
}

function validateBody(question: QuestionDraft, prefix: string): FieldError[] {
  switch (question.type) {
    case 'multipleChoiceSingle':
      return validateMultipleChoice(question, prefix, true);
    case 'multipleChoiceMulti':
      return validateMultipleChoice(question, prefix, false);
    case 'trueFalse':
      return validateTrueFalse(question.body, `${fieldPath(prefix, 'body')}.correct`);
    case 'shortText':
      return validateShortText(question.body, question.scoringMode, prefix);
    case 'longText':
      return validateLongText(question.body, prefix);
    case 'dragDropSharedBank':
      return validateSharedBank(question.body, question.scoringMode, prefix);
    case 'dragDropPerSlot':
      return validatePerSlot(question.body, question.scoringMode, prefix);
    case 'ordering':
      return validateOrdering(question.body, prefix);
  }
}

function validateMultipleChoice(question: QuestionDraft, prefix: string, single: boolean): FieldError[] {
  const errors: FieldError[] = [];
  const options = isMultipleChoiceBody(question.body) ? question.body.options : [];
  if (options.length < 2) {
    errors.push({
      path: `${fieldPath(prefix, 'body')}.options`,
      message: 'Multiple choice questions require at least two options.',
    });
  }
  errors.push(...validateUniqueIds(options.map((option) => option.id), `${fieldPath(prefix, 'body')}.options`, 'option'));
  let correctCount = 0;
  options.forEach((option, index) => {
    errors.push(...validateId(option.id, `${fieldPath(prefix, 'body')}.options.${index}.id`, 'Option'));
    errors.push(...validateText(option.text, `${fieldPath(prefix, 'body')}.options.${index}.text`, 'Option text'));
    if (option.isCorrect) {
      correctCount += 1;
    }
  });
  if (question.scoringMode === 'auto') {
    if (single && correctCount !== 1) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.options`,
        message: 'Multiple choice (single) auto scoring requires exactly one correct option.',
      });
    }
    if (!single && correctCount < 1) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.options`,
        message: 'Multiple choice (multi) auto scoring requires at least one correct option.',
      });
    }
  }
  return errors;
}

function validateTrueFalse(body: QuestionBodyDraft, path: string): FieldError[] {
  if (!isTrueFalseBody(body) || typeof body.correct !== 'boolean') {
    return [{ path, message: "True/false questions require a boolean 'correct' value." }];
  }
  return [];
}

function validateShortText(body: QuestionBodyDraft, scoring: ScoringMode, prefix: string): FieldError[] {
  const errors: FieldError[] = [];
  const answers = isShortTextBody(body) ? body.acceptableAnswers : [];
  const seen = new Set<string>();
  answers.forEach((row, index) => {
    if (typeof row.value !== 'string' || row.value.trim().length === 0) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.acceptableAnswers.${index}.value`,
        message: 'Acceptable answers cannot be empty.',
      });
      return;
    }
    const trimmed = row.value.trim();
    if (seen.has(trimmed)) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.acceptableAnswers.${index}.value`,
        message: 'Acceptable answers must be unique.',
      });
    }
    seen.add(trimmed);
  });
  if (scoring === 'auto' && seen.size === 0) {
    errors.push({
      path: `${fieldPath(prefix, 'body')}.acceptableAnswers`,
      message: 'Short text auto scoring requires at least one acceptable answer.',
    });
  }
  return errors;
}

function validateLongText(body: QuestionBodyDraft, prefix: string): FieldError[] {
  const errors: FieldError[] = [];
  if (!isLongTextBody(body)) {
    return errors;
  }
  if (body.maxLength !== null && body.maxLength !== undefined && `${body.maxLength}` !== '') {
    const maxLength = Number(body.maxLength);
    if (!Number.isInteger(maxLength) || maxLength < 1) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.maxLength`,
        message: 'Long text maxLength must be at least 1 when set.',
      });
    }
  }
  const guidance = (body.guidance ?? '').trim();
  if (guidance.length > QUESTION_STEM_MAX) {
    errors.push({
      path: `${fieldPath(prefix, 'body')}.guidance`,
      message: `Guidance cannot exceed ${QUESTION_STEM_MAX} characters.`,
    });
  }
  return errors;
}

function validateSharedBank(body: QuestionBodyDraft, scoring: ScoringMode, prefix: string): FieldError[] {
  const errors: FieldError[] = [];
  if (!isSharedBankBody(body)) {
    errors.push({ path: `${fieldPath(prefix, 'body')}`, message: 'Shared-bank drag-and-drop body is invalid.' });
    return errors;
  }
  if (body.slots.length === 0) {
    errors.push({
      path: `${fieldPath(prefix, 'body')}.slots`,
      message: 'Shared-bank drag-and-drop requires at least one slot.',
    });
  }
  if (body.bank.length === 0) {
    errors.push({
      path: `${fieldPath(prefix, 'body')}.bank`,
      message: 'Shared-bank drag-and-drop requires a non-empty bank.',
    });
  }
  errors.push(...validateUniqueIds(body.bank.map((item) => item.id), `${fieldPath(prefix, 'body')}.bank`, 'bank item'));
  errors.push(...validateUniqueIds(body.slots.map((slot) => slot.id), `${fieldPath(prefix, 'body')}.slots`, 'slot'));
  const nonDistractors = new Set<string>();
  const bankIds = new Set<string>();
  body.bank.forEach((item, index) => {
    errors.push(...validateId(item.id, `${fieldPath(prefix, 'body')}.bank.${index}.id`, 'Bank item'));
    errors.push(...validateText(item.text, `${fieldPath(prefix, 'body')}.bank.${index}.text`, 'Bank item text'));
    bankIds.add(item.id.trim());
    if (!item.isDistractor) {
      nonDistractors.add(item.id.trim());
    }
  });
  if (body.bank.length > 0 && nonDistractors.size === 0) {
    errors.push({
      path: `${fieldPath(prefix, 'body')}.bank`,
      message: 'Shared-bank drag-and-drop requires at least one non-distractor bank item.',
    });
  }
  body.slots.forEach((slot, index) => {
    errors.push(...validateId(slot.id, `${fieldPath(prefix, 'body')}.slots.${index}.id`, 'Slot'));
    errors.push(...validateText(slot.label, `${fieldPath(prefix, 'body')}.slots.${index}.label`, 'Slot label'));
    const correctItemId = slot.correctItemId.trim();
    if (!correctItemId) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.slots.${index}.correctItemId`,
        message: 'Slot correctItemId is required.',
      });
    } else if (scoring === 'auto' && !nonDistractors.has(correctItemId)) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.slots.${index}.correctItemId`,
        message: `Slot '${slot.id}' correctItemId must reference a non-distractor bank item.`,
      });
    } else if (scoring !== 'auto' && !bankIds.has(correctItemId)) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.slots.${index}.correctItemId`,
        message: `Slot '${slot.id}' correctItemId must reference a bank item.`,
      });
    }
  });
  return errors;
}

function validatePerSlot(body: QuestionBodyDraft, scoring: ScoringMode, prefix: string): FieldError[] {
  const errors: FieldError[] = [];
  if (!isPerSlotBody(body)) {
    errors.push({ path: `${fieldPath(prefix, 'body')}`, message: 'Per-slot drag-and-drop body is invalid.' });
    return errors;
  }
  if (body.slots.length === 0) {
    errors.push({
      path: `${fieldPath(prefix, 'body')}.slots`,
      message: 'Per-slot drag-and-drop requires at least one slot.',
    });
  }
  errors.push(...validateUniqueIds(body.slots.map((slot) => slot.id), `${fieldPath(prefix, 'body')}.slots`, 'slot'));
  body.slots.forEach((slot, slotIndex) => {
    errors.push(...validateId(slot.id, `${fieldPath(prefix, 'body')}.slots.${slotIndex}.id`, 'Slot'));
    errors.push(...validateText(slot.label, `${fieldPath(prefix, 'body')}.slots.${slotIndex}.label`, 'Slot label'));
    if (slot.options.length < 2) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.slots.${slotIndex}.options`,
        message: `Slot '${slot.id}' requires at least two options.`,
      });
    }
    errors.push(
      ...validateUniqueIds(
        slot.options.map((option) => option.id),
        `${fieldPath(prefix, 'body')}.slots.${slotIndex}.options`,
        `slot '${slot.id}' option`,
      ),
    );
    let correctCount = 0;
    slot.options.forEach((option, optionIndex) => {
      errors.push(
        ...validateId(option.id, `${fieldPath(prefix, 'body')}.slots.${slotIndex}.options.${optionIndex}.id`, 'Option'),
      );
      errors.push(
        ...validateText(
          option.text,
          `${fieldPath(prefix, 'body')}.slots.${slotIndex}.options.${optionIndex}.text`,
          'Option text',
        ),
      );
      if (option.isCorrect) {
        correctCount += 1;
      }
    });
    if (scoring === 'auto' && slot.options.length >= 2 && correctCount !== 1) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.slots.${slotIndex}.options`,
        message: `Slot '${slot.id}' auto scoring requires exactly one correct option.`,
      });
    }
  });
  return errors;
}

function validateOrdering(body: QuestionBodyDraft, prefix: string): FieldError[] {
  const errors: FieldError[] = [];
  if (!isOrderingBody(body)) {
    errors.push({ path: `${fieldPath(prefix, 'body')}`, message: 'Ordering body is invalid.' });
    return errors;
  }
  if (body.items.length < 2) {
    errors.push({
      path: `${fieldPath(prefix, 'body')}.items`,
      message: 'Ordering questions require at least two items.',
    });
  }
  errors.push(...validateUniqueIds(body.items.map((item) => item.id), `${fieldPath(prefix, 'body')}.items`, 'ordering item'));
  const indexes: number[] = [];
  body.items.forEach((item, index) => {
    errors.push(...validateId(item.id, `${fieldPath(prefix, 'body')}.items.${index}.id`, 'Ordering item'));
    errors.push(...validateText(item.text, `${fieldPath(prefix, 'body')}.items.${index}.text`, 'Ordering item text'));
    const correctIndex = Number(item.correctIndex);
    if (!Number.isInteger(correctIndex)) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.items.${index}.correctIndex`,
        message: 'Ordering correctIndex must be an integer.',
      });
    } else {
      indexes.push(correctIndex);
    }
  });
  if (body.items.length >= 2 && indexes.length === body.items.length) {
    const expected = body.items.map((_, index) => index);
    const sorted = [...indexes].sort((a, b) => a - b);
    if (sorted.some((value, index) => value !== expected[index])) {
      errors.push({
        path: `${fieldPath(prefix, 'body')}.items`,
        message: 'Ordering correctIndex values must be unique and cover 0..n-1.',
      });
    }
  }
  return errors;
}

function validateId(value: string, path: string, name: string): FieldError[] {
  if (!value || value.trim().length === 0) {
    return [{ path, message: `${name} id is required.` }];
  }
  if (value.trim().length > BODY_ID_MAX) {
    return [{ path, message: `${name} id cannot exceed ${BODY_ID_MAX} characters.` }];
  }
  return [];
}

function validateText(value: string, path: string, name: string): FieldError[] {
  if (!value || value.trim().length === 0) {
    return [{ path, message: `${name} is required.` }];
  }
  if (value.trim().length > QUESTION_STEM_MAX) {
    return [{ path, message: `${name} cannot exceed ${QUESTION_STEM_MAX} characters.` }];
  }
  return [];
}

function validateUniqueIds(ids: string[], path: string, noun: string): FieldError[] {
  const seen = new Set<string>();
  for (const id of ids) {
    const trimmed = id.trim();
    if (!trimmed) {
      continue;
    }
    if (seen.has(trimmed)) {
      return [{ path, message: `Duplicate ${noun} id '${trimmed}'.` }];
    }
    seen.add(trimmed);
  }
  return [];
}

export function toQuestionRequest(question: QuestionDraft, _sortOrder = 0): QuestionRequest {
  const request: QuestionRequest = {
    type: question.type,
    stem: question.stem.trim(),
    scoringMode: question.scoringMode,
    points: Number(question.points),
    body: toQuestionBody(question.type, question.body),
  };
  const id = question.id?.trim();
  if (id && isUuid(id)) {
    request.id = id;
  }
  if (requiresCreditMode(question.type) && question.creditMode) {
    request.creditMode = question.creditMode;
  }
  const sourceQuestionId = question.sourceQuestionId?.trim();
  if (sourceQuestionId && isUuid(sourceQuestionId)) {
    request.sourceQuestionId = sourceQuestionId;
  }
  return request;
}

function toQuestionBody(type: QuestionType, body: QuestionBodyDraft): QuestionBody {
  switch (type) {
    case 'multipleChoiceSingle':
    case 'multipleChoiceMulti':
      return {
        options: (isMultipleChoiceBody(body) ? body.options : []).map(toChoiceOption),
      } satisfies MultipleChoiceBody;
    case 'trueFalse':
      return {
        correct: isTrueFalseBody(body) ? Boolean(body.correct) : true,
      } satisfies TrueFalseBody;
    case 'shortText': {
      const answers = isShortTextBody(body) ? body.acceptableAnswers : [];
      return {
        acceptableAnswers: answers.map((row) => row.value.trim()).filter((value) => value.length > 0),
        caseSensitive: isShortTextBody(body) ? Boolean(body.caseSensitive) : false,
      } satisfies ShortTextBody;
    }
    case 'longText': {
      const draft = isLongTextBody(body) ? body : { maxLength: null, guidance: '' };
      const result: LongTextBody = {};
      const maxLength = draft.maxLength === null || draft.maxLength === undefined ? NaN : Number(draft.maxLength);
      if (Number.isInteger(maxLength) && maxLength >= 1) {
        result.maxLength = maxLength;
      }
      const guidance = (draft.guidance ?? '').trim();
      if (guidance) {
        result.guidance = guidance;
      }
      return result;
    }
    case 'dragDropSharedBank': {
      const draft = isSharedBankBody(body) ? body : { slots: [], bank: [] };
      return {
        slots: draft.slots.map(
          (slot) =>
            ({
              id: slot.id.trim(),
              label: slot.label.trim(),
              correctItemId: slot.correctItemId.trim(),
            }) satisfies SharedBankSlotBody,
        ),
        bank: draft.bank.map(
          (item) =>
            ({
              id: item.id.trim(),
              text: item.text.trim(),
              isDistractor: Boolean(item.isDistractor),
            }) satisfies BankItemBody,
        ),
      } satisfies DragDropSharedBankBody;
    }
    case 'dragDropPerSlot': {
      const draft = isPerSlotBody(body) ? body : { slots: [] };
      return {
        slots: draft.slots.map(
          (slot) =>
            ({
              id: slot.id.trim(),
              label: slot.label.trim(),
              options: slot.options.map(toChoiceOption),
            }) satisfies PerSlotBody,
        ),
      } satisfies DragDropPerSlotBody;
    }
    case 'ordering': {
      const draft = isOrderingBody(body) ? body : { items: [] };
      return {
        items: draft.items.map((item, index) => ({
          id: item.id.trim(),
          text: item.text.trim(),
          correctIndex: Number.isInteger(Number(item.correctIndex)) ? Number(item.correctIndex) : index,
        })),
      } satisfies OrderingBody;
    }
  }
}

function toChoiceOption(option: ChoiceOptionDraft): ChoiceOptionBody {
  return {
    id: option.id.trim(),
    text: option.text.trim(),
    isCorrect: Boolean(option.isCorrect),
  };
}

function tagsFromDraft(tags: TagDraft[]): Record<string, string> {
  const result: Record<string, string> = {};
  for (const tag of tags) {
    const key = tag.key.trim();
    if (key) {
      result[key] = tag.value.trim();
    }
  }
  return result;
}

export function questionToDraft(question: QuestionResponse): QuestionDraft {
  return {
    id: question.id,
    type: question.type,
    stem: question.stem,
    points: question.points,
    scoringMode: question.scoringMode,
    creditMode: requiresCreditMode(question.type) ? question.creditMode : null,
    sourceQuestionId: question.sourceQuestionId?.trim() ? question.sourceQuestionId : null,
    body: bodyToDraft(question.type, question.body),
  };
}

function bodyToDraft(type: QuestionType, body: QuestionBody): QuestionBodyDraft {
  switch (type) {
    case 'multipleChoiceSingle':
    case 'multipleChoiceMulti': {
      const options = 'options' in body && Array.isArray(body.options) ? body.options : [];
      return {
        options: options.map((option) => ({
          id: option.id,
          text: option.text,
          isCorrect: Boolean(option.isCorrect),
        })),
      };
    }
    case 'trueFalse':
      return { correct: 'correct' in body ? Boolean(body.correct) : true };
    case 'shortText': {
      const answers = 'acceptableAnswers' in body && Array.isArray(body.acceptableAnswers) ? body.acceptableAnswers : [];
      return {
        acceptableAnswers: answers.map((value) => ({ value })),
        caseSensitive: 'caseSensitive' in body ? Boolean(body.caseSensitive) : false,
      };
    }
    case 'longText':
      return {
        maxLength: 'maxLength' in body && typeof body.maxLength === 'number' ? body.maxLength : null,
        guidance: 'guidance' in body && typeof body.guidance === 'string' ? body.guidance : '',
      };
    case 'dragDropSharedBank': {
      const slots = 'slots' in body && Array.isArray(body.slots) ? body.slots : [];
      const bank = 'bank' in body && Array.isArray(body.bank) ? body.bank : [];
      return {
        slots: slots.map((slot) => ({
          id: 'id' in slot ? String(slot.id) : newItemId('slot'),
          label: 'label' in slot ? String(slot.label) : '',
          correctItemId: 'correctItemId' in slot ? String(slot.correctItemId) : '',
        })),
        bank: bank.map((item) => ({
          id: item.id,
          text: item.text,
          isDistractor: Boolean(item.isDistractor),
        })),
      };
    }
    case 'dragDropPerSlot': {
      const perSlot = body as DragDropPerSlotBody;
      const slots = Array.isArray(perSlot.slots) ? perSlot.slots : [];
      return {
        slots: slots.map((slot) => ({
          id: slot.id,
          label: slot.label,
          options: (slot.options ?? []).map((option) => ({
            id: option.id,
            text: option.text,
            isCorrect: Boolean(option.isCorrect),
          })),
        })),
      };
    }
    case 'ordering': {
      const items = 'items' in body && Array.isArray(body.items) ? body.items : [];
      return {
        items: items.map((item) => ({
          id: item.id,
          text: item.text,
          correctIndex: item.correctIndex,
        })),
      };
    }
  }
}

function isMultipleChoiceBody(body: QuestionBodyDraft): body is MultipleChoiceBodyDraft {
  return 'options' in body && Array.isArray(body.options) && !('label' in (body.options[0] ?? {}));
}

function isTrueFalseBody(body: QuestionBodyDraft): body is TrueFalseBodyDraft {
  return 'correct' in body;
}

function isShortTextBody(body: QuestionBodyDraft): body is ShortTextBodyDraft {
  return 'acceptableAnswers' in body;
}

function isLongTextBody(body: QuestionBodyDraft): body is LongTextBodyDraft {
  return 'guidance' in body || 'maxLength' in body;
}

function isSharedBankBody(body: QuestionBodyDraft): body is DragDropSharedBankBodyDraft {
  return 'bank' in body && 'slots' in body;
}

function isPerSlotBody(body: QuestionBodyDraft): body is DragDropPerSlotBodyDraft {
  return 'slots' in body && !('bank' in body) && !('items' in body);
}

function isOrderingBody(body: QuestionBodyDraft): body is OrderingBodyDraft {
  return 'items' in body;
}
