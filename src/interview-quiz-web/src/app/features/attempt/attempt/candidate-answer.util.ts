import { AnswerValue, CandidateQuestion, QuestionType } from '../../../core/api/contracts';

export interface ChoiceOptionView {
  id: string;
  text: string;
}

export interface SharedSlotView {
  id: string;
  label: string;
}

export interface BankItemView {
  id: string;
  text: string;
}

export interface PerSlotView {
  id: string;
  label: string;
  options: ChoiceOptionView[];
}

export interface OrderingItemView {
  id: string;
  text: string;
}

function asObject(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function asArray(value: unknown): unknown[] {
  return Array.isArray(value) ? value : [];
}

function asString(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

export function choiceOptions(question: CandidateQuestion): ChoiceOptionView[] {
  return asArray(question.body['options'])
    .map((item) => asObject(item))
    .filter((item) => asString(item['id']))
    .map((item) => ({ id: asString(item['id']), text: asString(item['text']) }));
}

export function sharedSlots(question: CandidateQuestion): SharedSlotView[] {
  return asArray(question.body['slots'])
    .map((item) => asObject(item))
    .filter((item) => asString(item['id']))
    .map((item) => ({ id: asString(item['id']), label: asString(item['label']) }));
}

export function bankItems(question: CandidateQuestion): BankItemView[] {
  return asArray(question.body['bank'])
    .map((item) => asObject(item))
    .filter((item) => asString(item['id']))
    .map((item) => ({ id: asString(item['id']), text: asString(item['text']) }));
}

export function perSlots(question: CandidateQuestion): PerSlotView[] {
  return asArray(question.body['slots'])
    .map((item) => asObject(item))
    .filter((item) => asString(item['id']))
    .map((item) => ({
      id: asString(item['id']),
      label: asString(item['label']),
      options: asArray(item['options'])
        .map((option) => asObject(option))
        .filter((option) => asString(option['id']))
        .map((option) => ({ id: asString(option['id']), text: asString(option['text']) })),
    }));
}

export function orderingItems(question: CandidateQuestion): OrderingItemView[] {
  return asArray(question.body['items'])
    .map((item) => asObject(item))
    .filter((item) => asString(item['id']))
    .map((item) => ({ id: asString(item['id']), text: asString(item['text']) }));
}

export function longTextGuidance(question: CandidateQuestion): string {
  return asString(question.body['guidance']);
}

export function longTextMaxLength(question: CandidateQuestion): number | null {
  const value = question.body['maxLength'];
  return typeof value === 'number' && value > 0 ? value : null;
}

export function initialAnswer(question: CandidateQuestion, saved?: AnswerValue): AnswerValue | undefined {
  if (saved) {
    return saved;
  }
  switch (question.type) {
    case 'multipleChoiceMulti':
      return { optionIds: [] };
    case 'ordering':
      return { itemIds: orderingItems(question).map((item) => item.id) };
    case 'dragDropSharedBank':
      return { slots: sharedSlots(question).map((slot) => ({ slotId: slot.id, itemId: '' })) };
    case 'dragDropPerSlot':
      return { slots: perSlots(question).map((slot) => ({ slotId: slot.id, optionId: '' })) };
    default:
      return undefined;
  }
}

export function optionIdOf(value: AnswerValue | undefined): string {
  if (value && 'optionId' in value && typeof value.optionId === 'string') {
    return value.optionId;
  }
  return '';
}

export function optionIdsOf(value: AnswerValue | undefined): string[] {
  if (value && 'optionIds' in value && Array.isArray(value.optionIds)) {
    return value.optionIds.filter((id): id is string => typeof id === 'string');
  }
  return [];
}

export function boolValueOf(value: AnswerValue | undefined): boolean | null {
  if (value && 'value' in value && typeof value.value === 'boolean') {
    return value.value;
  }
  return null;
}

export function textOf(value: AnswerValue | undefined): string {
  if (value && 'text' in value && typeof value.text === 'string') {
    return value.text;
  }
  return '';
}

export function itemIdsOf(value: AnswerValue | undefined, fallback: string[]): string[] {
  if (value && 'itemIds' in value && Array.isArray(value.itemIds) && value.itemIds.length > 0) {
    return value.itemIds.filter((id): id is string => typeof id === 'string');
  }
  return fallback;
}

export function slotItemId(value: AnswerValue | undefined, slotId: string): string {
  if (!value || !('slots' in value) || !Array.isArray(value.slots)) {
    return '';
  }
  const slot = value.slots.find((item) => item.slotId === slotId);
  return slot && 'itemId' in slot && typeof slot.itemId === 'string' ? slot.itemId : '';
}

export function slotOptionId(value: AnswerValue | undefined, slotId: string): string {
  if (!value || !('slots' in value) || !Array.isArray(value.slots)) {
    return '';
  }
  const slot = value.slots.find((item) => item.slotId === slotId);
  return slot && 'optionId' in slot && typeof slot.optionId === 'string' ? slot.optionId : '';
}

export function sanitizeForSave(type: QuestionType, value: AnswerValue): AnswerValue | null {
  switch (type) {
    case 'multipleChoiceSingle': {
      const optionId = optionIdOf(value);
      return optionId ? { optionId } : null;
    }
    case 'multipleChoiceMulti':
      return { optionIds: optionIdsOf(value) };
    case 'trueFalse': {
      const bool = boolValueOf(value);
      return bool === null ? null : { value: bool };
    }
    case 'shortText':
    case 'longText':
      return { text: textOf(value) };
    case 'dragDropSharedBank': {
      if (!('slots' in value) || !Array.isArray(value.slots)) {
        return null;
      }
      return {
        slots: value.slots
          .filter((slot) => slot.slotId && 'itemId' in slot && slot.itemId)
          .map((slot) => ({ slotId: slot.slotId, itemId: (slot as { itemId: string }).itemId })),
      };
    }
    case 'dragDropPerSlot': {
      if (!('slots' in value) || !Array.isArray(value.slots)) {
        return null;
      }
      return {
        slots: value.slots
          .filter((slot) => slot.slotId && 'optionId' in slot && slot.optionId)
          .map((slot) => ({ slotId: slot.slotId, optionId: (slot as { optionId: string }).optionId })),
      };
    }
    case 'ordering':
      return { itemIds: itemIdsOf(value, []) };
    default:
      return null;
  }
}
